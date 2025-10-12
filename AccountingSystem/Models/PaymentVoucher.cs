using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;
using AccountingSystem.Models.Workflows;

namespace AccountingSystem.Models
{
    public class PaymentVoucher
    {
        public int Id { get; set; }

        [Required]
        public int SupplierId { get; set; }

        // Account used when voucher is non-cash (credit account)
        public int? AccountId { get; set; }

        public int? AgentId { get; set; }

        [Required]
        public int CurrencyId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal ExchangeRate { get; set; } = 1m;

        public DateTime Date { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Notes { get; set; }

        public string CreatedById { get; set; } = string.Empty;

        public bool IsCash { get; set; }

        public PaymentVoucherStatus Status { get; set; } = PaymentVoucherStatus.Draft;

        public DateTime? ApprovedAt { get; set; }

        public string? ApprovedById { get; set; }

        public int? WorkflowInstanceId { get; set; }

        [ValidateNever]
        public virtual Supplier Supplier { get; set; } = null!;

        [ValidateNever]
        public virtual Account? Account { get; set; }

        [ValidateNever]
        public virtual Agent? Agent { get; set; }

        [ValidateNever]
        public virtual Currency Currency { get; set; } = null!;

        [ValidateNever]
        public virtual User CreatedBy { get; set; } = null!;

        [ValidateNever]
        public virtual User? ApprovedBy { get; set; }

        [ValidateNever]
        public virtual WorkflowInstance? WorkflowInstance { get; set; }
    }
}

