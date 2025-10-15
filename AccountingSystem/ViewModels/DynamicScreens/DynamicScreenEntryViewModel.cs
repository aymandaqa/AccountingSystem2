using System.Collections.Generic;
using AccountingSystem.Models.DynamicScreens;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels.DynamicScreens
{
    public class DynamicScreenEntryViewModel
    {
        public DynamicScreenDefinition Screen { get; set; } = null!;

        public DynamicScreenEntryInputModel Input { get; set; } = new();

        public List<DynamicScreenEntryFieldViewModel> Fields { get; set; } = new();

        public IEnumerable<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
    }

    public class DynamicScreenEntryFieldViewModel
    {
        public DynamicScreenField Field { get; set; } = null!;

        public IEnumerable<SelectListItem> Options { get; set; } = new List<SelectListItem>();
    }
}
