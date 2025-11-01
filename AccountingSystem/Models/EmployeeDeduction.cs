using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
    public class EmployeeDeduction
    {
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int DeductionTypeId { get; set; }

        [Range(0, double.MaxValue)]
        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Range(2000, 2100)]
        public int Year { get; set; } = DateTime.Now.Year;

        [Range(1, 12)]
        public int Month { get; set; } = DateTime.Now.Month;

        [StringLength(250)]
        public string? Description { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public virtual Employee Employee { get; set; } = null!;

        public virtual DeductionType DeductionType { get; set; } = null!;
    }
}
