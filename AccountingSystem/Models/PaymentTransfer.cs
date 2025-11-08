using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
    public class PaymentTransfer
    {
        public int Id { get; set; }

        [Required]
        public string SenderId { get; set; } = string.Empty;

        [Required]
        public string ReceiverId { get; set; } = string.Empty;

        [Required]
        public int FromPaymentAccountId { get; set; }

        [Required]
        public int ToPaymentAccountId { get; set; }

        public int? FromBranchId { get; set; }
        public int? ToBranchId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public string? CurrencyBreakdownJson { get; set; }

        [StringLength(260)]
        public string? AttachmentFileName { get; set; }

        [StringLength(500)]
        public string? AttachmentFilePath { get; set; }

        public TransferStatus Status { get; set; } = TransferStatus.Pending;

        public int? JournalEntryId { get; set; }
        public int? SenderJournalEntryId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual User Sender { get; set; } = null!;
        public virtual User Receiver { get; set; } = null!;
        public virtual Account FromPaymentAccount { get; set; } = null!;
        public virtual Account ToPaymentAccount { get; set; } = null!;
        public virtual Branch? FromBranch { get; set; }
        public virtual Branch? ToBranch { get; set; }
        public virtual JournalEntry? JournalEntry { get; set; }
        public virtual JournalEntry? SenderJournalEntry { get; set; }
    }

    public enum TransferStatus
    {
        Pending = 1,
        Accepted = 2,
        Rejected = 3
    }
}
