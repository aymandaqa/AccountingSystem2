using System;
using System.Collections.Generic;

namespace AccountingSystem.ViewModels
{
    public class UserJournalEntryDailyReportViewModel
    {
        public DateTime FromDate { get; set; } = DateTime.Today;
        public DateTime ToDate { get; set; } = DateTime.Today;
        public string? ReferenceFilter { get; set; }
        public List<UserJournalEntryDailyReportItem> Items { get; set; } = new();
    }

    public class UserJournalEntryDailyReportItem
    {
        public DateTime Date { get; set; }
        public string Reference { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal TotalCashImpact { get; set; }
        public List<UserJournalEntrySummary> Entries { get; set; } = new();
    }

    public class UserJournalEntrySummary
    {
        public int JournalEntryId { get; set; }
        public string Number { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public decimal CashImpactAmount { get; set; }
        public string TransactionTypeName { get; set; } = string.Empty;
        public List<UserJournalEntryLineSummary> Lines { get; set; } = new();
    }

    public class UserJournalEntryLineSummary
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
    }
}
