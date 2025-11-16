using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
    public class EmployeeLoanInstallment
    {
        public int Id { get; set; }

        [Required]
        public int EmployeeLoanId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue)]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue)]
        public decimal PaidAmount { get; set; }

        [DataType(DataType.Date)]
        public DateTime DueDate { get; set; }

        public LoanInstallmentStatus Status { get; set; } = LoanInstallmentStatus.Pending;

        public DateTime? PaidAt { get; set; }

        public int? PayrollBatchLineId { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual EmployeeLoan Loan { get; set; } = null!;

        public virtual PayrollBatchLine? PayrollBatchLine { get; set; }
    }

    public enum LoanInstallmentStatus
    {
        Pending = 1,
        Paid = 2,
        Rescheduled = 3
    }
}
