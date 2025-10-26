using System;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class JournalEntryLineReportViewModel
    {
        public DateTime FromDate { get; set; } = DateTime.Today.AddMonths(-1);
        public DateTime ToDate { get; set; } = DateTime.Today;
        public int? BranchId { get; set; }
        public int? AccountId { get; set; }
        public int? CostCenterId { get; set; }
        public JournalEntryStatus? Status { get; set; }
        public bool StatusFilterProvided { get; set; }
        public string SelectedStatusName { get; set; } = string.Empty;
        public string? SelectedBranchName { get; set; }
        public string? SelectedAccountName { get; set; }
        public string? SelectedAccountCurrencyCode { get; set; }
        public string? SelectedCostCenterName { get; set; }
        public string? SearchTerm { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public int ResultCount { get; set; }
        public bool FiltersApplied { get; set; }
        public int PageNumber { get; set; } = 1;
        public int PageSize { get; set; } = 50;
        public int TotalPages { get; set; }
        public List<JournalEntryLineReportItemViewModel> Lines { get; set; } = new();
        public List<SelectListItem> Branches { get; set; } = new();
        public List<SelectListItem> Accounts { get; set; } = new();
        public List<SelectListItem> CostCenters { get; set; } = new();
        public List<SelectListItem> StatusOptions { get; set; } = new();

        public bool HasResults => Lines.Count > 0;
        public decimal NetBalance => TotalDebit - TotalCredit;
        public int FirstItemIndex => !HasResults ? 0 : ((PageNumber - 1) * PageSize) + 1;
        public int LastItemIndex => !HasResults ? 0 : Math.Min(PageNumber * PageSize, ResultCount);
        public bool HasPreviousPage => PageNumber > 1;
        public bool HasNextPage => TotalPages > 0 && PageNumber < TotalPages;
    }

    public class JournalEntryLineReportItemViewModel
    {
        public int JournalEntryId { get; set; }
        public DateTime Date { get; set; }
        public string JournalEntryNumber { get; set; } = string.Empty;
        public string JournalEntryDescription { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public string? CostCenterName { get; set; }
        public string? LineDescription { get; set; }
        public string? Reference { get; set; }
        public string StatusDisplay { get; set; } = string.Empty;
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
    }
}
