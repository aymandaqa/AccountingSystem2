using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "assetdepreciations.view")]
    public class AssetDepreciationsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAssetDepreciationService _assetDepreciationService;
        private readonly UserManager<User> _userManager;

        public AssetDepreciationsController(
            ApplicationDbContext context,
            IAssetDepreciationService assetDepreciationService,
            UserManager<User> userManager)
        {
            _context = context;
            _assetDepreciationService = assetDepreciationService;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index(int assetId)
        {
            var asset = await _context.Assets
                .Include(a => a.AssetType)
                .Include(a => a.Branch)
                .Include(a => a.Depreciations)
                    .ThenInclude(d => d.JournalEntry)
                .Include(a => a.Depreciations)
                    .ThenInclude(d => d.CreatedBy)
                .FirstOrDefaultAsync(a => a.Id == assetId);

            if (asset == null)
            {
                return NotFound();
            }

            var depreciations = asset.Depreciations
                .OrderBy(d => d.PeriodNumber)
                .Select(d => new AssetDepreciationListViewModel
                {
                    Id = d.Id,
                    PeriodNumber = d.PeriodNumber,
                    PeriodStart = d.PeriodStart,
                    PeriodEnd = d.PeriodEnd,
                    Amount = d.Amount,
                    AccumulatedDepreciation = d.AccumulatedDepreciation,
                    BookValue = d.BookValue,
                    JournalEntryId = d.JournalEntryId,
                    JournalEntryNumber = d.JournalEntry?.Number,
                    CreatedAt = d.CreatedAt,
                    CreatedByName = d.CreatedBy != null ? d.CreatedBy.FullName : string.Empty
                }).ToList();

            var (nextStart, nextEnd) = GetNextPeriodWindow(asset);
            var hasRemaining = asset.AssetType.IsDepreciable && asset.DepreciationPeriods.HasValue && depreciations.Count < asset.DepreciationPeriods.Value;

            var model = new AssetDepreciationIndexViewModel
            {
                AssetId = asset.Id,
                AssetName = asset.Name,
                AssetTypeName = asset.AssetType.Name,
                BranchName = asset.Branch.NameAr,
                OriginalCost = asset.OriginalCost,
                SalvageValue = asset.SalvageValue,
                AccumulatedDepreciation = asset.AccumulatedDepreciation,
                BookValue = asset.BookValue,
                DepreciationFrequency = asset.DepreciationFrequency,
                DepreciationPeriods = asset.DepreciationPeriods,
                PurchaseDate = asset.PurchaseDate,
                Depreciations = depreciations,
                HasRemainingDepreciation = hasRemaining,
                NextPeriodStart = nextStart,
                NextPeriodEnd = nextEnd
            };

            ViewBag.AssetTypeIsDepreciable = asset.AssetType.IsDepreciable;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assetdepreciations.create")]
        public async Task<IActionResult> Calculate(int assetId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var result = await _assetDepreciationService.CalculateNextPeriodAsync(assetId, user.Id);
            if (result.Success)
            {
                TempData["Success"] = result.Message;
            }
            else
            {
                TempData["Error"] = result.Message;
            }

            return RedirectToAction(nameof(Index), new { assetId });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assetdepreciations.create")]
        public async Task<IActionResult> BulkPreview([FromForm] int[] assetIds, [FromForm] bool processAll = false)
        {
            List<int> ids;

            if (processAll)
            {
                ids = await _context.Assets
                    .AsNoTracking()
                    .Where(a => a.AssetType.IsDepreciable)
                    .Select(a => a.Id)
                    .ToListAsync();
            }
            else
            {
                ids = assetIds?.Distinct().ToList() ?? new List<int>();
            }

            if (ids.Count == 0)
            {
                TempData["Error"] = "لم يتم تحديد أي أصول لاحتساب الإهلاك.";
                return RedirectToAction("Index", "Assets");
            }

            var previewItems = new List<AssetDepreciationBulkPreviewItemViewModel>();
            foreach (var id in ids)
            {
                var preview = await _assetDepreciationService.PreviewNextPeriodAsync(id);
                var assetName = string.IsNullOrWhiteSpace(preview.AssetName)
                    ? $"أصل #{(preview.AssetId != 0 ? preview.AssetId : id)}"
                    : preview.AssetName;
                var item = new AssetDepreciationBulkPreviewItemViewModel
                {
                    AssetId = preview.AssetId,
                    AssetName = assetName,
                    AssetTypeName = string.IsNullOrWhiteSpace(preview.AssetTypeName) ? "-" : preview.AssetTypeName,
                    BranchName = string.IsNullOrWhiteSpace(preview.BranchName) ? "-" : preview.BranchName,
                    CanDepreciate = preview.Success,
                    Message = preview.Message
                };

                if (preview.Success && preview.Details != null)
                {
                    item.PeriodNumber = preview.Details.PeriodNumber;
                    item.PeriodStart = preview.Details.PeriodStart;
                    item.PeriodEnd = preview.Details.PeriodEnd;
                    item.Amount = preview.Details.Amount;
                    item.AccumulatedBefore = preview.Details.AccumulatedBefore;
                    item.AccumulatedAfter = preview.Details.AccumulatedAfter;
                    item.BookValueAfter = preview.Details.BookValueAfter;
                }

                previewItems.Add(item);
            }

            if (previewItems.All(i => !i.CanDepreciate))
            {
                TempData["Error"] = "لا توجد أصول متاحة لاحتساب الإهلاك.";
                return RedirectToAction("Index", "Assets");
            }

            var model = new AssetDepreciationBulkPreviewViewModel
            {
                Items = previewItems
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assetdepreciations.create")]
        public async Task<IActionResult> BulkConfirm([FromForm] int[] assetIds)
        {
            if (assetIds == null || assetIds.Length == 0)
            {
                TempData["Error"] = "لم يتم تحديد أي أصول لتأكيد الإهلاك.";
                return RedirectToAction("Index", "Assets");
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var ids = assetIds.Distinct().ToList();
            var assetNames = await _context.Assets
                .AsNoTracking()
                .Where(a => ids.Contains(a.Id))
                .Select(a => new { a.Id, a.Name })
                .ToDictionaryAsync(a => a.Id, a => a.Name);

            var successCount = 0;
            var failures = new List<string>();

            foreach (var id in ids)
            {
                var result = await _assetDepreciationService.CalculateNextPeriodAsync(id, user.Id);
                if (result.Success)
                {
                    successCount++;
                }
                else
                {
                    var name = assetNames.TryGetValue(id, out var assetName) ? assetName : $"#{id}";
                    failures.Add($"{name}: {result.Message}");
                }
            }

            if (successCount > 0)
            {
                TempData["Success"] = $"تم احتساب الإهلاك بنجاح لـ {successCount} أصل.";
            }

            if (failures.Count > 0)
            {
                TempData["Error"] = "تعذر احتساب الإهلاك لبعض الأصول: " + string.Join("، ", failures);
            }

            return RedirectToAction("Index", "Assets");
        }

        private static (DateTime? Start, DateTime? End) GetNextPeriodWindow(Asset asset)
        {
            if (!asset.DepreciationPeriods.HasValue || !asset.DepreciationFrequency.HasValue || !asset.PurchaseDate.HasValue)
            {
                return (null, null);
            }

            if (asset.Depreciations.Count >= asset.DepreciationPeriods.Value)
            {
                return (null, null);
            }

            var last = asset.Depreciations
                .OrderByDescending(d => d.PeriodNumber)
                .FirstOrDefault();

            var start = last != null ? last.PeriodEnd.AddDays(1) : asset.PurchaseDate.Value;
            DateTime end;
            if (asset.DepreciationFrequency == DepreciationFrequency.Monthly)
            {
                end = start.AddMonths(1).AddDays(-1);
                var maxEnd = asset.PurchaseDate.Value.AddMonths(asset.DepreciationPeriods.Value).AddDays(-1);
                if (end > maxEnd)
                {
                    end = maxEnd;
                }
            }
            else
            {
                end = start.AddYears(1).AddDays(-1);
                var maxEnd = asset.PurchaseDate.Value.AddYears(asset.DepreciationPeriods.Value).AddDays(-1);
                if (end > maxEnd)
                {
                    end = maxEnd;
                }
            }

            if (start > end)
            {
                return (null, null);
            }

            return (start, end);
        }
    }
}
