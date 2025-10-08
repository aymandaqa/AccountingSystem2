using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models;

namespace AccountingSystem.Models.Workflows
{
    public class WorkflowStep
    {
        public int Id { get; set; }

        public int WorkflowDefinitionId { get; set; }

        public int Order { get; set; }

        public WorkflowStepType StepType { get; set; }

        [StringLength(450)]
        public string? ApproverUserId { get; set; }

        public int? BranchId { get; set; }

        [StringLength(200)]
        public string? RequiredPermission { get; set; }

        public virtual WorkflowDefinition WorkflowDefinition { get; set; } = null!;

        public virtual User? ApproverUser { get; set; }

        public virtual Branch? Branch { get; set; }

        public virtual ICollection<WorkflowAction> Actions { get; set; } = new List<WorkflowAction>();
    }
}
