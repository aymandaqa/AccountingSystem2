using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class AllowanceType
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

        public virtual ICollection<EmployeeAllowance> EmployeeAllowances { get; set; } = new List<EmployeeAllowance>();

        public virtual ICollection<PayrollBatchLineAllowance> PayrollAllowances { get; set; } = new List<PayrollBatchLineAllowance>();
    }
}
