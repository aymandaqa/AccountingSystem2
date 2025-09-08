using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class Account
    {
        public int Id { get; set; }

        [Required]
        [StringLength(20)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string NameAr { get; set; } = string.Empty;

        [StringLength(200)]
        public string? NameEn { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public int? ParentId { get; set; }

        public int Level { get; set; } = 1;

        public AccountType AccountType { get; set; }

        public AccountNature Nature { get; set; }

        public AccountClassification Classification { get; set; }

        public AccountSubClassification SubClassification { get; set; }

        public decimal OpeningBalance { get; set; } = 0;

        [Required]
        public int CurrencyId { get; set; }

        // Calculated property for current balance
        public decimal CurrentBalance { get; set; } = 0;

        public bool IsActive { get; set; } = true;

        public bool CanHaveChildren { get; set; } = true;

        public bool CanPostTransactions { get; set; } = true;

        public int? BranchId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual Account? Parent { get; set; }
        public virtual ICollection<Account> Children { get; set; } = new List<Account>();
        public virtual Branch? Branch { get; set; }
        public virtual Currency Currency { get; set; } = null!;
        public virtual ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();
    }

    public enum AccountType
    {
        Assets = 1,
        Liabilities = 2,
        Equity = 3,
        Revenue = 4,
        Expenses = 5
    }

    public enum AccountNature
    {
        Debit = 1,
        Credit = 2
    }

    public enum AccountClassification
    {
        BalanceSheet = 1,
        IncomeStatement = 2
    }

    public enum AccountSubClassification
    {
        // Balance Sheet
        Assets = 1,
        Liabilities = 2,
        OwnerEquity = 3,
        
        // Income Statement
        Revenue = 4,
        Expense = 5
    }
}

