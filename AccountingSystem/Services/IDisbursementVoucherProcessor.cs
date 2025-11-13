using System.Threading;
using System.Threading.Tasks;
using AccountingSystem.Models;

namespace AccountingSystem.Services
{
    public interface IDisbursementVoucherProcessor
    {
        Task<JournalEntryPreview> BuildPreviewAsync(int voucherId, CancellationToken cancellationToken = default);
        Task FinalizeAsync(DisbursementVoucher voucher, string approvedById, CancellationToken cancellationToken = default);
    }
}
