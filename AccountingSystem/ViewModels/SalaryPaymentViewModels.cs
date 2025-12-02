using System;
using AccountingSystem.Models;

namespace AccountingSystem.ViewModels
{
    public class SalaryPaymentListItemViewModel
    {
        public required SalaryPayment Payment { get; init; }

        public string? JournalEntryNumber { get; init; }

        public string? JournalEntryReference { get; init; }
    }

    public class SalaryPaymentIndexViewModel : PaginatedListViewModel<SalaryPaymentListItemViewModel>
    {
        public int? BranchId { get; init; }
    }
}
