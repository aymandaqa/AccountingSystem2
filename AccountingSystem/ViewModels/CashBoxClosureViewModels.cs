using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.ViewModels
{
    public class CashBoxClosureCreateViewModel
    {
        [Required]
        [Display(Name = "المبلغ المعدود")]
        public decimal CountedAmount { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        public string AccountName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
    }
}
