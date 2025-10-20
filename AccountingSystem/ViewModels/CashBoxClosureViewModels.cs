using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class CashBoxClosureCreateViewModel
    {
        [Required]
        public int AccountId { get; set; }

        public List<SelectListItem> Accounts { get; set; } = new();

        public List<AccountOption> AccountOptions { get; set; } = new();

        [Required]
        [Display(Name = "المبلغ المعدود")]
        public decimal CountedAmount { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        public string AccountName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;

        [Display(Name = "الرصيد الافتتاحي")]
        public decimal OpeningBalance { get; set; }

        [Display(Name = "حركات اليوم")]
        public decimal TodayTransactions { get; set; }

        [Display(Name = "الرصيد التراكمي")]
        public decimal CumulativeBalance { get; set; }

        public decimal Difference => CountedAmount - TodayTransactions;

        public Dictionary<int, List<CurrencyUnitOption>> CurrencyUnits { get; set; } = new();

        public List<CurrencyUnitCountInput> CurrencyUnitCounts { get; set; } = new();

        public class AccountOption
        {
            public int AccountId { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public int CurrencyId { get; set; }
            public string CurrencyCode { get; set; } = string.Empty;
            public bool Selected { get; set; }
        }

        public class CurrencyUnitOption
        {
            public int CurrencyUnitId { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal ValueInBaseUnit { get; set; }
        }

        public class CurrencyUnitCountInput
        {
            public int CurrencyUnitId { get; set; }
            public int Count { get; set; }
        }
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
        public DateTime? ClosingDate { get; set; }
        public string UserName { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public decimal OpeningBalance { get; set; }
        public decimal CountedAmount { get; set; }
        public decimal ClosingBalance { get; set; }
        public decimal Difference { get; set; }
        public string DifferenceType { get; set; } = string.Empty;
        public string Status { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public string? ApprovalNotes { get; set; }
    }
}
