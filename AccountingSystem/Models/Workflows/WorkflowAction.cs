using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models;

namespace AccountingSystem.Models.Workflows
{
    public class WorkflowAction
    {
        public int Id { get; set; }

        public int WorkflowInstanceId { get; set; }

        public int WorkflowStepId { get; set; }

        public WorkflowActionStatus Status { get; set; } = WorkflowActionStatus.Pending;

        [StringLength(450)]
        public string? UserId { get; set; }

        public DateTime? ActionedAt { get; set; }

        [StringLength(1000)]
        public string? Notes { get; set; }

        public virtual WorkflowInstance WorkflowInstance { get; set; } = null!;

        public virtual WorkflowStep WorkflowStep { get; set; } = null!;

        public virtual User? User { get; set; }
    }
}
