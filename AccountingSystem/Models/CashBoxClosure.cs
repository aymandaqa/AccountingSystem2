using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class CashBoxClosure
    {
        public int Id { get; set; }

        [Required]
        public string UserId { get; set; } = string.Empty;

        [Required]
        public int AccountId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required]
        [Display(Name = "المبلغ المعدود")]
        public decimal CountedAmount { get; set; }

        [Display(Name = "الرصيد الافتتاحي")]
        public decimal OpeningBalance { get; set; }

        [Display(Name = "الرصيد الختامي")]
        public decimal ClosingBalance { get; set; }

        [Display(Name = "الملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "الحالة")]
        public CashBoxClosureStatus Status { get; set; } = CashBoxClosureStatus.Pending;

        [Display(Name = "السبب")]
        [StringLength(500)]
        public string? Reason { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? ApprovedAt { get; set; }

        public DateTime? ClosingDate { get; set; }

        // Navigation properties
        public virtual User? User { get; set; }
        public virtual Account? Account { get; set; }
        public virtual Branch? Branch { get; set; }
    }

    public enum CashBoxClosureStatus
    {
        Pending = 0,
        ApprovedMatched = 1,
        ApprovedWithDifference = 2,
        Rejected = 3
    }
}
