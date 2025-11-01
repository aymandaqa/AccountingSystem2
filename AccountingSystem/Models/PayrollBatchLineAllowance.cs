using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
    public class PayrollBatchLineAllowance
    {
        public int Id { get; set; }

        [Required]
        public int PayrollBatchLineId { get; set; }

        public int? AllowanceTypeId { get; set; }

        public int? AccountId { get; set; }

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [StringLength(100)]
        public string? Type { get; set; }

        [StringLength(250)]
        public string? Description { get; set; }

        public virtual PayrollBatchLine PayrollLine { get; set; } = null!;

        public virtual AllowanceType? AllowanceType { get; set; }

        public virtual Account? Account { get; set; }
    }
}
