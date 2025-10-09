using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
    public class Employee
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Address { get; set; }

        [StringLength(50)]
        public string? PhoneNumber { get; set; }

        [StringLength(50)]
        public string? NationalId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [DataType(DataType.Date)]
        public DateTime HireDate { get; set; } = DateTime.Today;

        [Range(0, double.MaxValue)]
        [DataType(DataType.Currency)]
        public decimal Salary { get; set; }

        [StringLength(200)]
        public string? JobTitle { get; set; }

        public int AccountId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public virtual Branch Branch { get; set; } = null!;

        public virtual Account Account { get; set; } = null!;

        [InverseProperty(nameof(PayrollBatchLine.Employee))]
        public virtual ICollection<PayrollBatchLine> PayrollLines { get; set; } = new List<PayrollBatchLine>();
    }
}
