using AccountingSystem.Models;

namespace AccountingSystem.ViewModels
{
    public class PaymentVoucherListItemViewModel
    {
        public PaymentVoucher Voucher { get; set; } = null!;

        public int? JournalEntryId { get; set; }

        public string? JournalEntryNumber { get; set; }

        public string? JournalEntryReference { get; set; }
    }
}
