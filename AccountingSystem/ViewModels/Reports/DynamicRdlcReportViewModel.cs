using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels.Reports
{
    public class DynamicRdlcReportViewModel
    {
        public List<DynamicRdlcReportDefinition> Reports { get; set; } = new();
        public Dictionary<string, List<SelectListItem>> Lookups { get; set; } = new();
    }

    public class DynamicRdlcReportDefinition
    {
        public string Key { get; set; } = string.Empty;
        public string Name { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public string ReportPath { get; set; } = string.Empty;
        public List<DynamicReportParameter> Parameters { get; set; } = new();
    }

    public class DynamicReportParameter
    {
        public DynamicReportParameter()
        {
        }

        public DynamicReportParameter(string name, string displayName, DynamicReportParameterType type, bool required = false)
        {
            Name = name;
            DisplayName = displayName;
            Type = type;
            IsRequired = required;
        }

        public string Name { get; set; } = string.Empty;
        public string DisplayName { get; set; } = string.Empty;
        public DynamicReportParameterType Type { get; set; }
        public bool IsRequired { get; set; }
    }

    public enum DynamicReportParameterType
    {
        Text,
        Number,
        DateTime,
        Lookup
    }
}
