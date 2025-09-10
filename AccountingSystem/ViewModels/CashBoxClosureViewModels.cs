using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class CashBoxClosureCreateViewModel
    {
        [Required]
        public int AccountId { get; set; }

        public List<SelectListItem> Accounts { get; set; } = new();

        [Required]
        [Display(Name = "المبلغ المعدود")]
        public decimal CountedAmount { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        public string AccountName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;

        [Display(Name = "الرصيد الافتتاحي")]
        public decimal OpeningBalance { get; set; }

        [Display(Name = "حركات اليوم")]
        public decimal TodayTransactions { get; set; }

        [Display(Name = "الرصيد التراكمي")]
        public decimal CumulativeBalance { get; set; }

        public decimal Difference => CountedAmount - TodayTransactions;
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
        public decimal Difference { get; set; }
        public string Status { get; set; } = string.Empty;
    }
}
