using System.Threading.Tasks;
using AccountingSystem.Models;

namespace AccountingSystem.Services
{
    public interface IAssetDepreciationService
    {
        Task<AssetDepreciationResult> CalculateNextPeriodAsync(int assetId, string userId);
    }

    public class AssetDepreciationResult
    {
        public bool Success { get; set; }
        public string Message { get; set; } = string.Empty;
        public AssetDepreciation? Depreciation { get; set; }
    }
}
