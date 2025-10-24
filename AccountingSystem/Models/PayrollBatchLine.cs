using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
    public class PayrollBatchLine
    {
        public int Id { get; set; }

        [Required]
        public int PayrollBatchId { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal GrossAmount { get; set; }

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal DeductionAmount { get; set; }

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        public virtual PayrollBatch PayrollBatch { get; set; } = null!;

        [InverseProperty(nameof(Employee.PayrollLines))]
        public virtual Employee Employee { get; set; } = null!;

        public virtual Branch Branch { get; set; } = null!;
    }
}
