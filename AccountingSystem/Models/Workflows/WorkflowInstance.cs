using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AccountingSystem.Models;

namespace AccountingSystem.Models.Workflows
{
    public class WorkflowInstance
    {
        public int Id { get; set; }

        public int WorkflowDefinitionId { get; set; }

        public WorkflowDocumentType DocumentType { get; set; }

        public int DocumentId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DocumentAmount { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal DocumentAmountInBase { get; set; }

        public int? DocumentCurrencyId { get; set; }

        public WorkflowInstanceStatus Status { get; set; } = WorkflowInstanceStatus.InProgress;

        public int CurrentStepOrder { get; set; } = 1;

        [StringLength(450)]
        public string InitiatorId { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? CompletedAt { get; set; }

        public virtual WorkflowDefinition WorkflowDefinition { get; set; } = null!;

        public virtual Currency? DocumentCurrency { get; set; }

        public virtual ICollection<WorkflowAction> Actions { get; set; } = new List<WorkflowAction>();

        public virtual User Initiator { get; set; } = null!;
    }
}
