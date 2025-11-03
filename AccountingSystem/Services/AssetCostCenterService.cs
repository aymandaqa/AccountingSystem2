using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services
{
    public class AssetCostCenterService : IAssetCostCenterService
    {
        private readonly ApplicationDbContext _context;
        private const string AssetCostCenterCodePrefix = "AS";

        public AssetCostCenterService(ApplicationDbContext context)
        {
            _context = context;
        }

        public Task EnsureCostCenterAsync(Asset asset, CancellationToken cancellationToken = default)
        {
            if (asset == null)
            {
                throw new ArgumentNullException(nameof(asset));
            }

            return EnsureCostCentersAsync(new[] { asset }, cancellationToken);
        }

        public async Task EnsureCostCentersAsync(IEnumerable<Asset> assets, CancellationToken cancellationToken = default)
        {
            if (assets == null)
            {
                throw new ArgumentNullException(nameof(assets));
            }

            var assetList = assets
                .Where(a => a != null && a.Id != 0)
                .ToList();

            if (!assetList.Any())
            {
                return;
            }

            var assetsNeedingCreation = new List<(Asset Asset, CostCenter CostCenter)>();

            foreach (var asset in assetList)
            {
                if (!asset.CostCenterId.HasValue)
                {
                    var baseCode = $"{AssetCostCenterCodePrefix}{asset.Id:D6}";
                    var code = await GenerateUniqueCodeAsync(baseCode, cancellationToken);

                    var description = string.IsNullOrWhiteSpace(asset.Notes)
                        ? $"مركز تكلفة للأصل {asset.Name}"
                        : asset.Notes;

                    var costCenter = new CostCenter
                    {
                        Code = code,
                        NameAr = asset.Name,
                        NameEn = asset.Name,
                        Description = description,
                        IsActive = true,
                    };

                    assetsNeedingCreation.Add((asset, costCenter));
                }
                else
                {
                    var costCenter = await _context.CostCenters
                        .FirstOrDefaultAsync(cc => cc.Id == asset.CostCenterId.Value, cancellationToken);

                    if (costCenter != null)
                    {
                        costCenter.NameAr = asset.Name;
                        costCenter.NameEn = asset.Name;
                        costCenter.Description = string.IsNullOrWhiteSpace(asset.Notes)
                            ? $"مركز تكلفة للأصل {asset.Name}"
                            : asset.Notes;
                        costCenter.IsActive = true;
                        costCenter.UpdatedAt = DateTime.Now;
                    }
                }
            }

            if (assetsNeedingCreation.Any())
            {
                _context.CostCenters.AddRange(assetsNeedingCreation.Select(x => x.CostCenter));
                await _context.SaveChangesAsync(cancellationToken);

                foreach (var (asset, costCenter) in assetsNeedingCreation)
                {
                    asset.CostCenterId = costCenter.Id;
                    asset.UpdatedAt = DateTime.Now;
                }
            }
        }

        public async Task RemoveCostCenterAsync(Asset asset, CancellationToken cancellationToken = default)
        {
            if (asset?.CostCenterId == null)
            {
                return;
            }

            var costCenter = await _context.CostCenters
                .Include(cc => cc.JournalEntryLines)
                .FirstOrDefaultAsync(cc => cc.Id == asset.CostCenterId, cancellationToken);

            if (costCenter != null && !costCenter.JournalEntryLines.Any())
            {
                _context.CostCenters.Remove(costCenter);
            }
        }

        private async Task<string> GenerateUniqueCodeAsync(string baseCode, CancellationToken cancellationToken)
        {
            var code = baseCode;
            var suffix = 0;

            while (await _context.CostCenters.AnyAsync(cc => cc.Code == code, cancellationToken))
            {
                suffix++;
                var suffixText = suffix.ToString();
                var maxBaseLength = Math.Max(0, 10 - suffixText.Length);
                var trimmedBase = baseCode.Length > maxBaseLength
                    ? baseCode[..maxBaseLength]
                    : baseCode;

                code = $"{trimmedBase}{suffixText}";
            }

            return code;
        }
    }
}
