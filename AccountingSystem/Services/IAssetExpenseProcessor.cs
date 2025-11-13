using System.Threading;
using System.Threading.Tasks;
using AccountingSystem.Models;

namespace AccountingSystem.Services
{
    public interface IAssetExpenseProcessor
    {
        Task<JournalEntryPreview> BuildPreviewAsync(int expenseId, CancellationToken cancellationToken = default);
        Task FinalizeAsync(AssetExpense expense, string approvedById, CancellationToken cancellationToken = default);
    }
}
