using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class AccountSettlementLineViewModel
    {
        public int LineId { get; set; }
        public DateTime Date { get; set; }
        public string JournalNumber { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string? Reference { get; set; }
    }

    public class AccountSettlementPairViewModel
    {
        public int PairId { get; set; }
        public DateTime CreatedAt { get; set; }
        public AccountSettlementLineViewModel DebitLine { get; set; } = null!;
        public AccountSettlementLineViewModel CreditLine { get; set; } = null!;
    }

    public class AccountSettlementIndexViewModel
    {
        public int? AccountId { get; set; }
        public string? AccountName { get; set; }
        public IEnumerable<SelectListItem> Accounts { get; set; } = Enumerable.Empty<SelectListItem>();
        public List<AccountSettlementLineViewModel> DebitLines { get; set; } = new();
        public List<AccountSettlementLineViewModel> CreditLines { get; set; } = new();
        public List<AccountSettlementPairViewModel> SettledPairs { get; set; } = new();
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
        public DateTime SettlementDate { get; set; } = DateTime.Now;
    }

    public class AccountSettlementRequest
    {
        public int AccountId { get; set; }
        public List<int> SelectedDebitIds { get; set; } = new();
        public List<int> SelectedCreditIds { get; set; } = new();
        public DateTime? SettlementDate { get; set; }
        public DateTime? FromDate { get; set; }
        public DateTime? ToDate { get; set; }
    }
}
