using AccountingSystem.Models;

namespace AccountingSystem.Services
{
    public interface IPaymentVoucherProcessor
    {
        Task FinalizeVoucherAsync(PaymentVoucher voucher, string approvedById, CancellationToken cancellationToken = default);
    }
}
