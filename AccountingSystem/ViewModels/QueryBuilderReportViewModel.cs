using Microsoft.AspNetCore.Mvc.Rendering;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.ViewModels;

public class QueryBuilderReportViewModel
{
    public List<SelectListItem> Datasets { get; set; } = new();
}

public class QueryDatasetInfoViewModel
{
    public string Key { get; set; } = string.Empty;
    public string Name { get; set; } = string.Empty;
    public string Description { get; set; } = string.Empty;
    public List<QueryDatasetFieldViewModel> Fields { get; set; } = new();
}

public class QueryDatasetFieldViewModel
{
    public string Field { get; set; } = string.Empty;
    public string Label { get; set; } = string.Empty;
    public string Type { get; set; } = string.Empty;
    public string Category { get; set; } = string.Empty;
}

public class ReportQueryListItemViewModel
{
    public int Id { get; set; }
    public string Name { get; set; } = string.Empty;
    public string DatasetKey { get; set; } = string.Empty;
    public DateTime UpdatedAt { get; set; }
}

public class SaveReportQueryRequest
{
    [Required]
    public string Name { get; set; } = string.Empty;

    [Required]
    public string DatasetKey { get; set; } = string.Empty;

    [Required]
    public string RulesJson { get; set; } = string.Empty;

    public string? SelectedColumnsJson { get; set; }

    public int? Id { get; set; }
}

public class DeleteReportQueryRequest
{
    public int Id { get; set; }
}

public class ExecuteReportQueryRequest
{
    public string DatasetKey { get; set; } = string.Empty;
    public string? RulesJson { get; set; }
    public List<string>? Columns { get; set; }
}
