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
            var asset = await _context.Assets
                .Include(a => a.AssetType)
                    .ThenInclude(t => t.DepreciationExpenseAccount)
                .Include(a => a.AssetType)
                    .ThenInclude(t => t.AccumulatedDepreciationAccount)
                .Include(a => a.AssetType)
                    .ThenInclude(t => t.Account)
                .Include(a => a.Depreciations)
                .Include(a => a.Branch)
                .FirstOrDefaultAsync(a => a.Id == assetId);

            if (asset == null)
            {
                return new AssetDepreciationResult { Success = false, Message = "الأصل غير موجود" };
            }

            if (asset.AssetType == null || !asset.AssetType.IsDepreciable)
            {
                return new AssetDepreciationResult { Success = false, Message = "نوع الأصل غير قابل للإهلاك" };
            }

            if (!asset.AssetType.DepreciationExpenseAccountId.HasValue || !asset.AssetType.AccumulatedDepreciationAccountId.HasValue)
            {
                return new AssetDepreciationResult { Success = false, Message = "نوع الأصل لا يحتوي على إعدادات حسابات الإهلاك" };
            }

            if (!asset.OriginalCost.HasValue || !asset.DepreciationPeriods.HasValue || !asset.DepreciationFrequency.HasValue || !asset.PurchaseDate.HasValue)
            {
                return new AssetDepreciationResult { Success = false, Message = "بيانات الإهلاك غير مكتملة لهذا الأصل" };
            }

            var depreciableBase = asset.OriginalCost.Value - (asset.SalvageValue ?? 0m);
            if (depreciableBase <= 0)
            {
                return new AssetDepreciationResult { Success = false, Message = "لا يمكن احتساب الإهلاك لقيمة أصل أقل من أو مساوية لقيمة الخردة" };
            }

            var totalPeriods = asset.DepreciationPeriods.Value;
            var completedPeriods = asset.Depreciations.Count;
            if (completedPeriods >= totalPeriods)
            {
                return new AssetDepreciationResult { Success = false, Message = "تم احتساب جميع فترات الإهلاك" };
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
                return new AssetDepreciationResult { Success = false, Message = "لا توجد فترات متبقية للاهلاك" };
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
                return new AssetDepreciationResult { Success = false, Message = "لا يوجد مبلغ إهلاك متبقي" };
            }

            var accumulatedAfter = accumulatedBefore + amount;
            var bookValue = (asset.OriginalCost ?? 0m) - accumulatedAfter;
            if (asset.SalvageValue.HasValue && bookValue < asset.SalvageValue.Value)
            {
                bookValue = asset.SalvageValue.Value;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var lines = new[]
                {
                    new JournalEntryLine { AccountId = asset.AssetType.DepreciationExpenseAccountId!.Value, DebitAmount = amount },
                    new JournalEntryLine { AccountId = asset.AssetType.AccumulatedDepreciationAccountId!.Value, CreditAmount = amount }
                };

                var description = $"إثبات إهلاك الأصل: {asset.Name} - الفترة رقم {periodNumber}";
                var reference = $"ASSET-DEPR:{asset.Id}:{periodNumber}";

                var journalEntry = await _journalEntryService.CreateJournalEntryAsync(
                    periodEnd,
                    description,
                    asset.BranchId,
                    userId,
                    lines,
                    JournalEntryStatus.Posted,
                    reference: reference);

                var depreciation = new AssetDepreciation
                {
                    AssetId = asset.Id,
                    PeriodNumber = periodNumber,
                    PeriodStart = periodStart,
                    PeriodEnd = periodEnd,
                    Amount = amount,
                    AccumulatedDepreciation = accumulatedAfter,
                    BookValue = bookValue,
                    JournalEntryId = journalEntry.Id,
                    CreatedById = userId,
                    CreatedAt = DateTime.Now
                };

                asset.AccumulatedDepreciation = accumulatedAfter;
                asset.BookValue = bookValue;
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
    }
}
