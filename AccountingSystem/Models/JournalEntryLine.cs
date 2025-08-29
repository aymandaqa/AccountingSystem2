using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class JournalEntryLine
    {
        public int Id { get; set; }

        public int JournalEntryId { get; set; }

        public int AccountId { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public decimal DebitAmount { get; set; } = 0;

        public decimal CreditAmount { get; set; } = 0;

        public int? CostCenterId { get; set; }

        [StringLength(100)]
        public string? Reference { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation properties
        public virtual JournalEntry JournalEntry { get; set; } = null!;
        public virtual Account Account { get; set; } = null!;
        public virtual CostCenter? CostCenter { get; set; }
    }
}

