using System;
using System.Threading.Tasks;
using AccountingSystem.Models;

namespace AccountingSystem.Services
{
    public interface IAssetDepreciationService
    {
        Task<AssetDepreciationResult> CalculateNextPeriodAsync(int assetId, string userId);
        Task<AssetDepreciationPreviewResult> PreviewNextPeriodAsync(int assetId);
    }

    public class AssetDepreciationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public AssetDepreciation? Depreciation { get; set; }
    }

    public class AssetDepreciationPreviewResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public int AssetId { get; set; }
        public string AssetName { get; set; } = string.Empty;
        public string AssetTypeName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public AssetDepreciationPreviewDetails? Details { get; set; }
    }

    public class AssetDepreciationPreviewDetails
    {
        public int PeriodNumber { get; set; }
        public DateTime PeriodStart { get; set; }
        public DateTime PeriodEnd { get; set; }
        public decimal Amount { get; set; }
        public decimal AccumulatedBefore { get; set; }
        public decimal AccumulatedAfter { get; set; }
        public decimal BookValueAfter { get; set; }
    }
}
