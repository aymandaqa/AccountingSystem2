using AccountingSystem.Models;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public interface IAssetCostCenterService
    {
        Task EnsureCostCenterAsync(Asset asset, CancellationToken cancellationToken = default);

        Task EnsureCostCentersAsync(IEnumerable<Asset> assets, CancellationToken cancellationToken = default);

        Task RemoveCostCenterAsync(Asset asset, CancellationToken cancellationToken = default);
    }
}
