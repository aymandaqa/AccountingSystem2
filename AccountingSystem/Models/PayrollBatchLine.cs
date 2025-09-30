using System.ComponentModel.DataAnnotations;

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
        public decimal Amount { get; set; }

        public virtual PayrollBatch PayrollBatch { get; set; } = null!;

        public virtual Employee Employee { get; set; } = null!;

        public virtual Branch Branch { get; set; } = null!;
    }
}
