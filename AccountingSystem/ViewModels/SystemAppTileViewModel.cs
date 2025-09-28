using System.Collections.Generic;

namespace AccountingSystem.ViewModels
{
    public class SystemAppTileViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public string Icon { get; set; } = string.Empty;
        public string AccentColor { get; set; } = "#0d6efd";
        public string Permission { get; set; } = string.Empty;
        public string Url { get; set; } = string.Empty;
        public bool HasAccess { get; set; }
    }

    public class SystemAppOverviewViewModel
    {
        public List<SystemAppTileViewModel> Apps { get; set; } = new();
    }
}
