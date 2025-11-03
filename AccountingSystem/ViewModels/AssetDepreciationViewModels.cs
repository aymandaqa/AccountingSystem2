using System;
using System.Collections.Generic;
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
}
