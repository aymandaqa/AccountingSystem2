using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

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

    public class CashBoxClosureReportViewModel
    {
        public int? AccountId { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public List<SelectListItem> Accounts { get; set; } = new List<SelectListItem>();
        public List<CashBoxClosureReportItemViewModel> Closures { get; set; } = new List<CashBoxClosureReportItemViewModel>();
    }

    public class CashBoxClosureReportItemViewModel
    {
        public DateTime CreatedAt { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public decimal OpeningBalance { get; set; }
        public decimal CountedAmount { get; set; }
        public decimal ClosingBalance { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
