using System;
using System.Collections.Generic;
using AccountingSystem.Models.Workflows;

namespace AccountingSystem.ViewModels.Workflows
{
    public class WorkflowApprovalReportViewModel
    {
        public List<WorkflowApprovalReportItem> Items { get; set; } = new();
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public WorkflowActionStatus? Status { get; set; }
    }

    public class WorkflowApprovalReportItem
    {
        public int ActionId { get; set; }
        public int DocumentId { get; set; }
        public WorkflowDocumentType DocumentType { get; set; }
        public string Title { get; set; } = string.Empty;
        public string? RequesterName { get; set; }
        public string? ApproverName { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ActionedAt { get; set; }
        public WorkflowActionStatus Status { get; set; }
        public string? Notes { get; set; }
    }
}
