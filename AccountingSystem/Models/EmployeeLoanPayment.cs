using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
    public class EmployeeLoanPayment
    {
        public int Id { get; set; }

        [Required]
        public int EmployeeLoanId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0.01, double.MaxValue)]
        public decimal Amount { get; set; }

        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [StringLength(500)]
        public string? Notes { get; set; }

        [StringLength(450)]
        public string CreatedById { get; set; } = string.Empty;

        public int? JournalEntryId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual EmployeeLoan Loan { get; set; } = null!;

        public virtual JournalEntry? JournalEntry { get; set; }
    }
}
