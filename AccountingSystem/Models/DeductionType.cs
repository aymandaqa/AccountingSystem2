using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class DeductionType
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        [Required]
        public int AccountId { get; set; }

        public bool IsActive { get; set; } = true;

        public virtual Account Account { get; set; } = null!;

        public virtual ICollection<EmployeeDeduction> EmployeeDeductions { get; set; } = new List<EmployeeDeduction>();

        public virtual ICollection<PayrollBatchLineDeduction> PayrollDeductions { get; set; } = new List<PayrollBatchLineDeduction>();
    }
}
