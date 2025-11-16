using System;
using AccountingSystem.Models.Workflows;

namespace AccountingSystem.ViewModels.Dashboard
{
    public class PendingWorkflowRequestViewModel
    {
        public int WorkflowInstanceId { get; set; }

        public int DocumentId { get; set; }

        public WorkflowDocumentType DocumentType { get; set; }

        public string Title { get; set; } = string.Empty;

        public string Description { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string? CurrencyCode { get; set; }

        public DateTime CreatedAt { get; set; }

        public string PendingWith { get; set; } = string.Empty;
    }
}
