namespace AccountingSystem.ViewModels
{
    public class UserAccountBalanceViewModel
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public decimal CurrentBalance { get; set; }
        public bool IsAgentAccount { get; set; }
    }
}
