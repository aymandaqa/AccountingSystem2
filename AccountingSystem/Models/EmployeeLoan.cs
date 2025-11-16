using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
    public class EmployeeLoan
    {
        public int Id { get; set; }

        [Required]
        public int EmployeeId { get; set; }

        [Required]
        public int AccountId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue)]
        public decimal PrincipalAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        [Range(0, double.MaxValue)]
        public decimal InstallmentAmount { get; set; }

        [Range(1, int.MaxValue)]
        public int InstallmentCount { get; set; }

        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; }

        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        [Required]
        public LoanInstallmentFrequency Frequency { get; set; } = LoanInstallmentFrequency.Monthly;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        [StringLength(450)]
        public string CreatedById { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;

        public virtual Employee Employee { get; set; } = null!;

        public virtual Account Account { get; set; } = null!;

        public virtual User? CreatedBy { get; set; }

        public virtual ICollection<EmployeeLoanInstallment> Installments { get; set; } = new List<EmployeeLoanInstallment>();
    }

    public enum LoanInstallmentFrequency
    {
        Monthly = 1,
        Weekly = 2
    }
}
