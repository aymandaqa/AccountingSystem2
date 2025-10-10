using Microsoft.AspNetCore.Identity;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
    public class User : IdentityUser
    {
        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [StringLength(200)]
        public string? FullName => $"{FirstName} {LastName}";

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? LastLoginAt { get; set; }

        public int? PaymentAccountId { get; set; }
        public int? PaymentBranchId { get; set; }
        public string? DriverAccountBranchIds { get; set; }
        public string? BusinessAccountBranchIds { get; set; }
        [Column(TypeName = "decimal(18,2)")]
        public decimal ExpenseLimit { get; set; } = 0;

        public string? SidebarMenuOrder { get; set; }

        // Navigation properties
        public virtual ICollection<UserBranch> UserBranches { get; set; } = new List<UserBranch>();
        public virtual ICollection<UserPermission> UserPermissions { get; set; } = new List<UserPermission>();
        public virtual ICollection<UserPermissionGroup> UserPermissionGroups { get; set; } = new List<UserPermissionGroup>();
        public virtual ICollection<JournalEntry> CreatedJournalEntries { get; set; } = new List<JournalEntry>();
        public virtual ICollection<UserPaymentAccount> UserPaymentAccounts { get; set; } = new List<UserPaymentAccount>();
        public virtual Account? PaymentAccount { get; set; }
        public virtual Branch? PaymentBranch { get; set; }
        public virtual ICollection<Expense> Expenses { get; set; } = new List<Expense>();
        public virtual ICollection<CashBoxClosure> CashBoxClosures { get; set; } = new List<CashBoxClosure>();
    }
}

