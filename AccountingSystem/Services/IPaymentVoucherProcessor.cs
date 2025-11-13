using System.Threading;
using System.Threading.Tasks;
using AccountingSystem.Models;

namespace AccountingSystem.Services
{
    public interface IPaymentVoucherProcessor
    {
        Task<JournalEntryPreview> BuildPreviewAsync(int voucherId, CancellationToken cancellationToken = default);
        Task FinalizeVoucherAsync(PaymentVoucher voucher, string approvedById, CancellationToken cancellationToken = default);
    }
}
