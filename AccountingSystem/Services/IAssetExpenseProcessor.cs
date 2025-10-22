using AccountingSystem.Models;
using System.Threading;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public interface IAssetExpenseProcessor
    {
        Task FinalizeAsync(AssetExpense expense, string approvedById, CancellationToken cancellationToken = default);
    }
}
