using AccountingSystem.Models;
using System.Threading;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public interface IDisbursementVoucherProcessor
    {
        Task FinalizeAsync(DisbursementVoucher voucher, string approvedById, CancellationToken cancellationToken = default);
    }
}
