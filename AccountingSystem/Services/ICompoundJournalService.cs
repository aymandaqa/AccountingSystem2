using System.Collections.Generic;
using AccountingSystem.Models;
using AccountingSystem.Models.CompoundJournals;

namespace AccountingSystem.Services
{
    public interface ICompoundJournalService
    {
        Task<CompoundJournalTemplate> ParseTemplateAsync(string templateJson, CancellationToken cancellationToken = default);

        Task<CompoundJournalExecutionResult> ExecuteAsync(int definitionId, CompoundJournalExecutionRequest request, CancellationToken cancellationToken = default);

        DateTime? CalculateNextRun(CompoundJournalDefinition definition, DateTime fromUtc);
    }

    public class CompoundJournalExecutionRequest
    {
        public string UserId { get; set; } = string.Empty;

        public DateTime ExecutionDate { get; set; } = DateTime.UtcNow;

        public DateTime? JournalDate { get; set; }

        public string? DescriptionOverride { get; set; }

        public string? ReferenceOverride { get; set; }

        public int? BranchIdOverride { get; set; }

        public JournalEntryStatus? StatusOverride { get; set; }

        public IDictionary<string, string>? ContextOverrides { get; set; }

        public bool IsAutomatic { get; set; }
    }

    public class CompoundJournalExecutionResult
    {
        public bool Success { get; set; }

        public int? JournalEntryId { get; set; }

        public string? Message { get; set; }

        public CompoundJournalExecutionStatus Status { get; set; }

        public Dictionary<string, string> ContextSnapshot { get; set; } = new();
    }
}
