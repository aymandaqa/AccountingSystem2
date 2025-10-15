using System.Collections.Generic;
using AccountingSystem.Models.DynamicScreens;

namespace AccountingSystem.ViewModels.DynamicScreens
{
    public class DynamicScreenEntriesIndexViewModel
    {
        public DynamicScreenDefinition Screen { get; set; } = null!;

        public IReadOnlyList<DynamicScreenEntry> Entries { get; set; } = new List<DynamicScreenEntry>();

        public bool CanManage { get; set; }
    }
}
