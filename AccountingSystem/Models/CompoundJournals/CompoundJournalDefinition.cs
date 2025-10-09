using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AccountingSystem.Models;

namespace AccountingSystem.Models.CompoundJournals
{
    public class CompoundJournalDefinition
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(200)]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        public string? Description { get; set; }

        [Required]
        public string TemplateJson { get; set; } = "{}";

        public CompoundJournalTriggerType TriggerType { get; set; } = CompoundJournalTriggerType.Manual;

        public bool IsActive { get; set; } = true;

        public DateTime? StartDateUtc { get; set; }

        public DateTime? EndDateUtc { get; set; }

        public DateTime? NextRunUtc { get; set; }

        public CompoundJournalRecurrence? Recurrence { get; set; }

        public int? RecurrenceInterval { get; set; }

        public DateTime? LastRunUtc { get; set; }

        public string CreatedById { get; set; } = string.Empty;

        public DateTime CreatedAtUtc { get; set; } = DateTime.UtcNow;

        [ForeignKey(nameof(CreatedById))]
        public virtual User CreatedBy { get; set; } = null!;

        public virtual ICollection<CompoundJournalExecutionLog> ExecutionLogs { get; set; } = new List<CompoundJournalExecutionLog>();
    }

    public enum CompoundJournalTriggerType
    {
        Manual = 1,
        OneTime = 2,
        Recurring = 3
    }

    public enum CompoundJournalRecurrence
    {
        Daily = 1,
        Weekly = 2,
        Monthly = 3,
        Yearly = 4
    }
}
