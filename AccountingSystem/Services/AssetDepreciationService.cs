using System;
using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services
{
    public class AssetDepreciationService : IAssetDepreciationService
    {
        private readonly ApplicationDbContext _context;
        private readonly IJournalEntryService _journalEntryService;

        public AssetDepreciationService(ApplicationDbContext context, IJournalEntryService journalEntryService)
        {
            _context = context;
            _journalEntryService = journalEntryService;
        }

        public async Task<AssetDepreciationResult> CalculateNextPeriodAsync(int assetId, string userId)
        {
            var (asset, preview) = await PrepareNextPeriodAsync(assetId, asTracking: true);
            if (preview == null)
            {
                return new AssetDepreciationResult
                {
                    Success = false,
                    Message = "حدث خطأ أثناء احتساب الإهلاك"
                };
            }

            if (!preview.Success || asset == null || preview.Details == null)
            {
                return new AssetDepreciationResult
                {
                    Success = false,
                    Message = preview?.Message ?? "حدث خطأ أثناء احتساب الإهلاك"
                };
            }

            var details = preview.Details;

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var lines = new[]
                {
                    new JournalEntryLine { AccountId = asset.AssetType.DepreciationExpenseAccountId!.Value, DebitAmount = details.Amount },
                    new JournalEntryLine { AccountId = asset.AssetType.AccumulatedDepreciationAccountId!.Value, CreditAmount = details.Amount }
                };

                var description = $"إثبات إهلاك الأصل: {asset.Name} - الفترة رقم {details.PeriodNumber}";
                var reference = $"ASSET-DEPR:{asset.Id}:{details.PeriodNumber}";

                var journalEntry = await _journalEntryService.CreateJournalEntryAsync(
                    details.PeriodEnd,
                    description,
                    asset.BranchId,
                    userId,
                    lines,
                    JournalEntryStatus.Posted,
                    reference: reference);

                var depreciation = new AssetDepreciation
                {
                    AssetId = asset.Id,
                    PeriodNumber = details.PeriodNumber,
                    PeriodStart = details.PeriodStart,
                    PeriodEnd = details.PeriodEnd,
                    Amount = details.Amount,
                    AccumulatedDepreciation = details.AccumulatedAfter,
                    BookValue = details.BookValueAfter,
                    JournalEntryId = journalEntry.Id,
                    CreatedById = userId,
                    CreatedAt = DateTime.Now
                };

                asset.AccumulatedDepreciation = details.AccumulatedAfter;
                asset.BookValue = details.BookValueAfter;
                asset.UpdatedAt = DateTime.Now;

                _context.AssetDepreciations.Add(depreciation);
                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                return new AssetDepreciationResult
                {
                    Success = true,
                    Message = "تم احتساب الإهلاك بنجاح",
                    Depreciation = depreciation
                };
            }
            catch
            {
                await transaction.RollbackAsync();
                return new AssetDepreciationResult
                {
                    Success = false,
                    Message = "حدث خطأ أثناء احتساب الإهلاك"
                };
            }
        }

        public async Task<AssetDepreciationPreviewResult> PreviewNextPeriodAsync(int assetId)
        {
            var (_, preview) = await PrepareNextPeriodAsync(assetId, asTracking: false);
            return preview ?? new AssetDepreciationPreviewResult
            {
                Success = false,
                Message = "حدث خطأ أثناء تحضير بيانات الإهلاك",
                AssetId = assetId
            };
        }

        private async Task<(Asset? Asset, AssetDepreciationPreviewResult? Preview)> PrepareNextPeriodAsync(int assetId, bool asTracking)
        {
            var query = _context.Assets
                .Include(a => a.AssetType)
                    .ThenInclude(t => t.DepreciationExpenseAccount)
                .Include(a => a.AssetType)
                    .ThenInclude(t => t.AccumulatedDepreciationAccount)
                .Include(a => a.AssetType)
                    .ThenInclude(t => t.Account)
                .Include(a => a.Depreciations)
                .Include(a => a.Branch)
                .Where(a => a.Id == assetId);

            if (!asTracking)
            {
                query = query.AsNoTracking();
            }

            var asset = await query.FirstOrDefaultAsync();

            if (asset == null)
            {
                return (null, new AssetDepreciationPreviewResult
                {
                    Success = false,
                    Message = "الأصل غير موجود",
                    AssetId = assetId
                });
            }

            var preview = new AssetDepreciationPreviewResult
            {
                AssetId = asset.Id,
                AssetName = asset.Name,
                AssetTypeName = asset.AssetType?.Name ?? string.Empty,
                BranchName = asset.Branch?.NameAr ?? string.Empty
            };

            if (asset.IsDisposed)
            {
                preview.Success = false;
                preview.Message = "تم بيع الأصل ولا يمكن متابعة الإهلاك.";
                return (asset, preview);
            }

            if (asset.AssetType == null || !asset.AssetType.IsDepreciable)
            {
                preview.Success = false;
                preview.Message = "نوع الأصل غير قابل للإهلاك";
                return (asset, preview);
            }

            if (!asset.AssetType.DepreciationExpenseAccountId.HasValue || !asset.AssetType.AccumulatedDepreciationAccountId.HasValue)
            {
                preview.Success = false;
                preview.Message = "نوع الأصل لا يحتوي على إعدادات حسابات الإهلاك";
                return (asset, preview);
            }

            if (!asset.OriginalCost.HasValue || !asset.DepreciationPeriods.HasValue || !asset.DepreciationFrequency.HasValue || !asset.PurchaseDate.HasValue)
            {
                preview.Success = false;
                preview.Message = "بيانات الإهلاك غير مكتملة لهذا الأصل";
                return (asset, preview);
            }

            var depreciableBase = asset.OriginalCost.Value - (asset.SalvageValue ?? 0m);
            if (depreciableBase <= 0)
            {
                preview.Success = false;
                preview.Message = "لا يمكن احتساب الإهلاك لقيمة أصل أقل من أو مساوية لقيمة الخردة";
                return (asset, preview);
            }

            var totalPeriods = asset.DepreciationPeriods.Value;
            var completedPeriods = asset.Depreciations.Count;
            if (completedPeriods >= totalPeriods)
            {
                preview.Success = false;
                preview.Message = "تم احتساب جميع فترات الإهلاك";
                return (asset, preview);
            }

            var periodNumber = completedPeriods + 1;
            var lastDepreciation = asset.Depreciations
                .OrderByDescending(d => d.PeriodNumber)
                .FirstOrDefault();

            var periodStart = lastDepreciation != null
                ? lastDepreciation.PeriodEnd.AddDays(1)
                : asset.PurchaseDate.Value;

            DateTime periodEnd;
            if (asset.DepreciationFrequency == DepreciationFrequency.Monthly)
            {
                periodEnd = periodStart.AddMonths(1).AddDays(-1);
                var maxEnd = asset.PurchaseDate.Value.AddMonths(totalPeriods).AddDays(-1);
                if (periodEnd > maxEnd)
                {
                    periodEnd = maxEnd;
                }
            }
            else
            {
                periodEnd = periodStart.AddYears(1).AddDays(-1);
                var maxEnd = asset.PurchaseDate.Value.AddYears(totalPeriods).AddDays(-1);
                if (periodEnd > maxEnd)
                {
                    periodEnd = maxEnd;
                }
            }

            if (periodStart > periodEnd)
            {
                preview.Success = false;
                preview.Message = "لا توجد فترات متبقية للاهلاك";
                return (asset, preview);
            }

            var standardAmount = Math.Round(depreciableBase / totalPeriods, 2, MidpointRounding.AwayFromZero);
            var accumulatedBefore = asset.AccumulatedDepreciation;
            var amount = standardAmount;
            var remaining = depreciableBase - accumulatedBefore;
            if (amount > remaining)
            {
                amount = remaining;
            }

            if (amount <= 0)
            {
                preview.Success = false;
                preview.Message = "لا يوجد مبلغ إهلاك متبقي";
                return (asset, preview);
            }

            var accumulatedAfter = accumulatedBefore + amount;
            var bookValue = (asset.OriginalCost ?? 0m) - accumulatedAfter;
            if (asset.SalvageValue.HasValue && bookValue < asset.SalvageValue.Value)
            {
                bookValue = asset.SalvageValue.Value;
            }

            preview.Success = true;
            preview.Message = "سيتم ترحيل الإهلاك التالي عند التأكيد";
            preview.Details = new AssetDepreciationPreviewDetails
            {
                PeriodNumber = periodNumber,
                PeriodStart = periodStart,
                PeriodEnd = periodEnd,
                Amount = amount,
                AccumulatedBefore = accumulatedBefore,
                AccumulatedAfter = accumulatedAfter,
                BookValueAfter = bookValue
            };

            return (asset, preview);
        }
    }
}
