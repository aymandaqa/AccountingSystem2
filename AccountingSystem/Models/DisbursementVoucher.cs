using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AccountingSystem.Models.Workflows;

namespace AccountingSystem.Models
{
    public class DisbursementVoucher
    {
        public int Id { get; set; }

        [Required]
        public int SupplierId { get; set; }

        [Required]
        public int AccountId { get; set; }

        [Required]
        public int CurrencyId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal ExchangeRate { get; set; } = 1m;

        public DateTime Date { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(260)]
        public string? AttachmentFileName { get; set; }

        [StringLength(500)]
        public string? AttachmentFilePath { get; set; }

        public string CreatedById { get; set; } = string.Empty;

        public DisbursementVoucherStatus Status { get; set; } = DisbursementVoucherStatus.PendingApproval;

        public DateTime? ApprovedAt { get; set; }

        public string? ApprovedById { get; set; }

        public int? WorkflowInstanceId { get; set; }

        public virtual Supplier Supplier { get; set; } = null!;
        public virtual Account Account { get; set; } = null!;
        public virtual Currency Currency { get; set; } = null!;
        public virtual User CreatedBy { get; set; } = null!;
        public virtual User? ApprovedBy { get; set; }
        public virtual WorkflowInstance? WorkflowInstance { get; set; }
    }
}
