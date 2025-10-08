using AccountingSystem.Models;
using AccountingSystem.Models.Workflows;
using System;

namespace AccountingSystem.ViewModels.Workflows
{
    public class WorkflowApprovalViewModel
    {
        public int ActionId { get; set; }
        public WorkflowDocumentType DocumentType { get; set; }
        public int DocumentId { get; set; }
        public string Title { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public PaymentVoucher? PaymentVoucher { get; set; }
    }
}
