using System;
using System.Collections.Generic;
using System.Linq;
using AccountingSystem.Models;

namespace AccountingSystem.ViewModels
{
    public class AssetDepreciationListViewModel
    {
        public int Id { get; set; }
        public int PeriodNumber { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal Amount { get; set; }
        public decimal AccumulatedDepreciation { get; set; }
        public decimal BookValue { get; set; }
        public int JournalEntryId { get; set; }
        public string? JournalEntryNumber { get; set; }
        public DateTime CreatedAt { get; set; }
        public string CreatedByName { get; set; } = string.Empty;
    }

    public class AssetDepreciationIndexViewModel
    {
        public int AssetId { get; set; }
        public string AssetName { get; set; } = string.Empty;
        public string AssetTypeName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public decimal? OriginalCost { get; set; }
        public decimal? SalvageValue { get; set; }
        public decimal AccumulatedDepreciation { get; set; }
        public decimal BookValue { get; set; }
        public DepreciationFrequency? DepreciationFrequency { get; set; }
        public int? DepreciationPeriods { get; set; }
        public DateTime? PurchaseDate { get; set; }
        public IReadOnlyList<AssetDepreciationListViewModel> Depreciations { get; set; } = Array.Empty<AssetDepreciationListViewModel>();
        public bool HasRemainingDepreciation { get; set; }
        public DateTime? NextPeriodStart { get; set; }
        public DateTime? NextPeriodEnd { get; set; }
    }

    public class AssetDepreciationBulkPreviewItemViewModel
    {
        public int AssetId { get; set; }
        public string AssetName { get; set; } = string.Empty;
        public string AssetTypeName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public bool CanDepreciate { get; set; }
        public string Message { get; set; } = string.Empty;
        public int? PeriodNumber { get; set; }
        public DateTime? PeriodStart { get; set; }
        public DateTime? PeriodEnd { get; set; }
        public decimal? Amount { get; set; }
        public decimal? AccumulatedBefore { get; set; }
        public decimal? AccumulatedAfter { get; set; }
        public decimal? BookValueAfter { get; set; }
    }

    public class AssetDepreciationBulkPreviewViewModel
    {
        public IReadOnlyList<AssetDepreciationBulkPreviewItemViewModel> Items { get; set; } = Array.Empty<AssetDepreciationBulkPreviewItemViewModel>();

        public bool HasConfirmableItems => Items.Any(i => i.CanDepreciate);

        public decimal TotalAmount => Items
            .Where(i => i.CanDepreciate && i.Amount.HasValue)
            .Sum(i => i.Amount!.Value);

        public int ConfirmableCount => Items.Count(i => i.CanDepreciate);

        public int FailedCount => Items.Count(i => !i.CanDepreciate);
    }
}
