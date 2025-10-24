using System;
using System.Collections.Generic;

namespace AccountingSystem.ViewModels
{
    public class UserDailyTransactionReportViewModel
    {
        public DateTime SelectedDate { get; set; }

        public List<UserDailyTransactionReportItem> Items { get; set; } = new();
    }

    public class UserDailyTransactionReportItem
    {
        public string Key { get; set; } = string.Empty;

        public string? Reference { get; set; }
        public string TypeDisplay { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public DateTime CreatedAt { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public UserDailyTransactionDocumentInfo? Document { get; set; }
        public List<UserDailyTransactionEntryViewModel> Entries { get; set; } = new();
    }

    public class UserDailyTransactionEntryViewModel
    {
        public int JournalEntryId { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public List<UserDailyTransactionLineViewModel> Lines { get; set; } = new();
    }

    public class UserDailyTransactionLineViewModel
    {
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string? CostCenter { get; set; }
        public string? Notes { get; set; }
    }

    public class UserDailyTransactionDocumentInfo
    {
        public string Title { get; set; } = string.Empty;
        public List<UserDailyTransactionDocumentField> Fields { get; set; } = new();
        public string? Notes { get; set; }
    }

    public class UserDailyTransactionDocumentField
    {
        public UserDailyTransactionDocumentField()
        {
        }

        public UserDailyTransactionDocumentField(string label, string value)
        {
            Label = label;
            Value = value;
        }

        public string Label { get; set; } = string.Empty;
        public string Value { get; set; } = string.Empty;
    }
}
