using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AccountingSystem.Models
{
    public class SalaryPayment
    {
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int PaymentAccountId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Required]
        public int CurrencyId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public DateTime Date { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Notes { get; set; }

        [Required]
        [StringLength(450)]
        public string CreatedById { get; set; } = string.Empty;

        public int? JournalEntryId { get; set; }

        [StringLength(50)]
        public string? ReferenceNumber { get; set; }

        [ValidateNever]
        public virtual Employee Employee { get; set; } = null!;

        [ValidateNever]
        public virtual Account PaymentAccount { get; set; } = null!;

        [ValidateNever]
        public virtual Branch Branch { get; set; } = null!;

        [ValidateNever]
        public virtual Currency Currency { get; set; } = null!;

        [ValidateNever]
        public virtual User CreatedBy { get; set; } = null!;

        [ValidateNever]
        public virtual JournalEntry? JournalEntry { get; set; }
    }
}
