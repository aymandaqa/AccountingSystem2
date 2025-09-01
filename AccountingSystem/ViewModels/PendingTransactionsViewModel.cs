using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;

namespace AccountingSystem.ViewModels
{
    public class PendingTransactionsViewModel
    {
        public DateTime FromDate { get; set; } = DateTime.Now.AddMonths(-1);
        public DateTime ToDate { get; set; } = DateTime.Now;
        public int? BranchId { get; set; }
        public List<TrialBalanceAccountViewModel> Accounts { get; set; } = new List<TrialBalanceAccountViewModel>();
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public decimal TotalDebits { get; set; }
        public decimal TotalCredits { get; set; }
    }
}

