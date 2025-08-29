using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class JournalEntry
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Number { get; set; } = string.Empty;

        [Required]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Reference { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public decimal TotalDebit { get; set; } = 0;

        public decimal TotalCredit { get; set; } = 0;

        public bool IsBalanced => TotalDebit == TotalCredit;

        public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;

        public int BranchId { get; set; }

        public string CreatedById { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public string? ApprovedById { get; set; }

        public DateTime? ApprovedAt { get; set; }

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Branch Branch { get; set; } = null!;
        public virtual User CreatedBy { get; set; } = null!;
        public virtual User? ApprovedBy { get; set; }
        public virtual ICollection<JournalEntryLine> Lines { get; set; } = new List<JournalEntryLine>();
    }

    public enum JournalEntryStatus
    {
        Draft = 1,
        Posted = 2,
        Approved = 3,
        Cancelled = 4
    }
}

