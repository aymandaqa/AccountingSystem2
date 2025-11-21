using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models.Workflows;

namespace AccountingSystem.ViewModels.Workflows
{
    public class WorkflowDefinitionViewModel
    {
        public int? Id { get; set; }

        [Required]
        [Display(Name = "اسم المخطط")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "الفرع")]
        public int? BranchId { get; set; }

        public WorkflowDocumentType DocumentType { get; set; } = WorkflowDocumentType.PaymentVoucher;

        [Display(Name = "آلية الاعتماد")]
        public WorkflowApprovalMode ApprovalMode { get; set; } = WorkflowApprovalMode.Linear;

        public List<WorkflowStepInputModel> Steps { get; set; } = new();
    }

    public class WorkflowStepInputModel
    {
        public int? Id { get; set; }
        public int Order { get; set; }
        public WorkflowStepConnector Connector { get; set; } = WorkflowStepConnector.And;
        public int? ParentOrder { get; set; }
        public int? ParentStepId { get; set; }
        public WorkflowStepType StepType { get; set; }
        public string? ApproverUserId { get; set; }
        public int? BranchId { get; set; }
        public string? RequiredPermission { get; set; }
        public decimal? MinAmount { get; set; }
        public decimal? MaxAmount { get; set; }
    }
}
