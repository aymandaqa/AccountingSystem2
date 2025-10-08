using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AccountingSystem.Models;

namespace AccountingSystem.Models.Workflows
{
    public class WorkflowDefinition
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        public WorkflowDocumentType DocumentType { get; set; }

        public bool IsActive { get; set; } = true;

        public int? BranchId { get; set; }

        [StringLength(450)]
        public string? CreatedById { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        [ForeignKey(nameof(BranchId))]
        public virtual Branch? Branch { get; set; }

        public virtual ICollection<WorkflowStep> Steps { get; set; } = new List<WorkflowStep>();
    }
}
