using System;
using System.Collections.Generic;
using AccountingSystem.Models;

namespace AccountingSystem.ViewModels
{
    public class AccountBalanceDiscrepancyViewModel
    {
        public DateTime GeneratedAt { get; set; } = DateTime.Now;
        public List<AccountBalanceDiscrepancyItemViewModel> Accounts { get; set; } = new();
        public decimal TotalCurrentBalance { get; set; }
        public decimal TotalLedgerBalance { get; set; }
        public decimal TotalDifference { get; set; }
        public decimal TotalAbsoluteDifference { get; set; }
    }

    public class AccountBalanceDiscrepancyItemViewModel
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal OpeningBalance { get; set; }
        public decimal CurrentBalance { get; set; }
        public decimal LedgerBalance { get; set; }
        public decimal Difference { get; set; }
        public AccountNature Nature { get; set; }
        public List<AccountBalanceDiscrepancyEntryViewModel> Entries { get; set; } = new();
    }

    public class AccountBalanceDiscrepancyEntryViewModel
    {
        public int JournalEntryId { get; set; }
        public string JournalNumber { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public decimal NetImpact { get; set; }
    }
}
