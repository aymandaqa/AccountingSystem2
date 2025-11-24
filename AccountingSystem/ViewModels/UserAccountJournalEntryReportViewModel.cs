using System;
using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class UserAccountJournalEntryReportViewModel
    {
        public DateTime FromDate { get; set; } = DateTime.Today;
        public DateTime ToDate { get; set; } = DateTime.Today;
        public string? ReferenceFilter { get; set; }
        public int? SelectedAccountId { get; set; }
        public List<SelectListItem> Accounts { get; set; } = new();
        public List<UserJournalEntryDailyReportItem> Items { get; set; } = new();
    }
}
