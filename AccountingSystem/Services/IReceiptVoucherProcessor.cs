using System.Threading;
using System.Threading.Tasks;
using AccountingSystem.Models;

namespace AccountingSystem.Services
{
    public interface IReceiptVoucherProcessor
    {
        Task<JournalEntryPreview> BuildPreviewAsync(int voucherId, CancellationToken cancellationToken = default);
        Task FinalizeAsync(ReceiptVoucher voucher, string approvedById, CancellationToken cancellationToken = default);
    }
}
