using AccountingSystem.Models;
using System.Threading;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public interface IReceiptVoucherProcessor
    {
        Task FinalizeAsync(ReceiptVoucher voucher, string approvedById, CancellationToken cancellationToken = default);
    }
}
