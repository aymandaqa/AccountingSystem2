using System.Collections.Generic;

namespace AccountingSystem.ViewModels
{
    public class AgentListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? BranchName { get; set; }
        public string? Address { get; set; }
        public int? AccountId { get; set; }
        public string? AccountCode { get; set; }
        public string? AccountName { get; set; }
        public decimal? AccountBalance { get; set; }
        public string? AccountCurrencyCode { get; set; }
    }

    public class AgentsIndexViewModel
    {
        public List<AgentListItemViewModel> Agents { get; set; } = new();
        public int CurrentPage { get; set; }
        public int PageSize { get; set; }
        public int TotalPages { get; set; }
        public int TotalCount { get; set; }
    }
}
