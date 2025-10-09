using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using AccountingSystem.Models;

namespace AccountingSystem.Models.CompoundJournals
{
    public class CompoundJournalExecutionLog
    {
        public int Id { get; set; }

        public int DefinitionId { get; set; }

        public DateTime ExecutedAtUtc { get; set; } = DateTime.UtcNow;

        public bool IsAutomatic { get; set; }

        public CompoundJournalExecutionStatus Status { get; set; } = CompoundJournalExecutionStatus.Success;

        [MaxLength(2000)]
        public string? Message { get; set; }

        public int? JournalEntryId { get; set; }

        public string? ContextSnapshotJson { get; set; }

        [ForeignKey(nameof(DefinitionId))]
        public virtual CompoundJournalDefinition Definition { get; set; } = null!;

        [ForeignKey(nameof(JournalEntryId))]
        public virtual JournalEntry? JournalEntry { get; set; }
    }

    public enum CompoundJournalExecutionStatus
    {
        Success = 1,
        Skipped = 2,
        Failed = 3
    }
}
