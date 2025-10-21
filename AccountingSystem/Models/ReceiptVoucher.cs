using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AccountingSystem.Models.Workflows;

namespace AccountingSystem.Models
{
    public class ReceiptVoucher
    {
        public int Id { get; set; }

        [Required]
        public int AccountId { get; set; }

        [Required]
        public int PaymentAccountId { get; set; }

        public int? SupplierId { get; set; }

        [Required]
        public int CurrencyId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal ExchangeRate { get; set; } = 1m;

        public DateTime Date { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Notes { get; set; }

        [Required]
        public string CreatedById { get; set; } = string.Empty;

        public ReceiptVoucherStatus Status { get; set; } = ReceiptVoucherStatus.PendingApproval;

        public DateTime? ApprovedAt { get; set; }

        public string? ApprovedById { get; set; }

        public int? WorkflowInstanceId { get; set; }

        public virtual Account Account { get; set; } = null!;
        public virtual Account PaymentAccount { get; set; } = null!;
        public virtual Currency Currency { get; set; } = null!;
        public virtual Supplier? Supplier { get; set; }
        public virtual User CreatedBy { get; set; } = null!;
        public virtual User? ApprovedBy { get; set; }
        public virtual WorkflowInstance? WorkflowInstance { get; set; }
    }
}
