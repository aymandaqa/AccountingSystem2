using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

namespace AccountingSystem.ViewModels
{
    public class UserCashTransactionReportViewModel
    {
        public string SelectedType { get; set; } = "all";
        public int? SelectedAccountId { get; set; }
        public List<SelectListItem> Accounts { get; set; } = new();
        public List<UserCashTransactionReportItem> Items { get; set; } = new();
    }

    public class UserCashTransactionReportItem
    {
        public DateTime Date { get; set; }
        public CashTransactionType Type { get; set; }
        public int? AccountId { get; set; }
        public string AccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Reference { get; set; } = string.Empty;
        public string? Notes { get; set; }
        public int? JournalEntryId { get; set; }
        public string? JournalEntryNumber { get; set; }

        public string TypeDisplay => Type switch
        {
            CashTransactionType.Payment => "سند دفع",
            CashTransactionType.Receipt => "سند قبض",
            _ => "غير معروف"
        };
    }

    public enum CashTransactionType
    {
        Receipt,
        Payment
    }
}
