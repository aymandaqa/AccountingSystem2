using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class JournalEntryManagementViewModel
    {
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Statuses { get; set; } = new List<SelectListItem>();
        public List<int> PageSizes { get; set; } = new List<int> { 25, 50, 100, 200 };
        public int DefaultPageSize { get; set; } = 50;
    }
}
