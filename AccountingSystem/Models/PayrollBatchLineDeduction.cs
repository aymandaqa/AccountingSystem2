using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
    public class PayrollBatchLineDeduction
    {
        public int Id { get; set; }

        [Required]
        public int PayrollBatchLineId { get; set; }

        public int? DeductionTypeId { get; set; }

        public int? AccountId { get; set; }

        [StringLength(100)]
        public string? Type { get; set; }

        [StringLength(250)]
        public string? Description { get; set; }

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [InverseProperty(nameof(PayrollBatchLine.Deductions))]
        public PayrollBatchLine PayrollLine { get; set; } = null!;

        public DeductionType? DeductionType { get; set; }

        public Account? Account { get; set; }
    }
}
