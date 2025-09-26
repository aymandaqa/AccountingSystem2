using System;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class DynamicPivotReportViewModel
    {
        public List<SelectListItem> ReportTypes { get; set; } = new();
    }

    public class PivotReportListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public DynamicReportType ReportType { get; set; }
        public DateTime UpdatedAt { get; set; }
    }

    public class SavePivotReportRequest
    {
        public int? Id { get; set; }
        public DynamicReportType ReportType { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Layout { get; set; } = string.Empty;
    }

    public class DeletePivotReportRequest
    {
        public int Id { get; set; }
    }
}
