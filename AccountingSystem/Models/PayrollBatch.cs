using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class PayrollBatch
    {
        public int Id { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required]
        public int PaymentAccountId { get; set; }

        public int Year { get; set; }

        public int Month { get; set; }

        [Required]
        public PayrollBatchStatus Status { get; set; } = PayrollBatchStatus.Draft;

        public decimal TotalAmount { get; set; }

        public string CreatedById { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public string? ConfirmedById { get; set; }

        public DateTime? ConfirmedAt { get; set; }

        [StringLength(200)]
        public string? ReferenceNumber { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public virtual Branch Branch { get; set; } = null!;

        public virtual Account PaymentAccount { get; set; } = null!;

        public virtual User CreatedBy { get; set; } = null!;

        public virtual User? ConfirmedBy { get; set; }

        public virtual ICollection<PayrollBatchLine> Lines { get; set; } = new List<PayrollBatchLine>();
    }

    public enum PayrollBatchStatus
    {
        Draft = 1,
        Confirmed = 2,
        Cancelled = 3
    }
}
