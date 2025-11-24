using AccountingSystem.Models;

namespace AccountingSystem.ViewModels
{
    public class DisbursementVoucherListItemViewModel
    {
        public DisbursementVoucher Voucher { get; set; } = null!;

        public int? JournalEntryId { get; set; }

        public string? JournalEntryNumber { get; set; }

        public string? RejectionReason { get; set; }
    }
}
