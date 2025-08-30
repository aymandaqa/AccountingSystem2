using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class Expense
    {
        public int Id { get; set; }
        public string UserId { get; set; } = string.Empty;
        public int PaymentAccountId { get; set; }
        public int BranchId { get; set; }
        public int ExpenseAccountId { get; set; }
        public decimal Amount { get; set; }
        [StringLength(500)]
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;
        public bool IsApproved { get; set; } = false;
        public int? JournalEntryId { get; set; }

        public virtual User User { get; set; } = null!;
        public virtual Account PaymentAccount { get; set; } = null!;
        public virtual Account ExpenseAccount { get; set; } = null!;
        public virtual Branch Branch { get; set; } = null!;
        public virtual JournalEntry? JournalEntry { get; set; }
    }
}
