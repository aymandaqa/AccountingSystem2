using System;

namespace AccountingSystem.ViewModels
{
    public class TransactionListItemViewModel
    {
        public string Type { get; set; } = string.Empty;

        public string TypeKey { get; set; } = string.Empty;

        public int Id { get; set; }

        public DateTime Date { get; set; }

        public string BranchName { get; set; } = string.Empty;

        public string CurrencyCode { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string? Counterparty { get; set; }

        public string? Notes { get; set; }

        public string? Status { get; set; }

        public string DetailsController { get; set; } = string.Empty;

        public string DetailsAction { get; set; } = "Details";

        public string? CreatedByName { get; set; }
    }

    public class TransactionSummaryItemViewModel
    {
        public string Label { get; set; } = string.Empty;

        public int Count { get; set; }

        public decimal TotalAmount { get; set; }
    }

    public class TransactionIndexViewModel : PaginatedListViewModel<TransactionListItemViewModel>
    {
        public required IReadOnlyList<TransactionSummaryItemViewModel> TypeSummaries { get; init; }

        public required IReadOnlyList<TransactionSummaryItemViewModel> StatusSummaries { get; init; }

        public string? TypeFilter { get; init; }

        public string? StatusFilter { get; init; }
    }
}

