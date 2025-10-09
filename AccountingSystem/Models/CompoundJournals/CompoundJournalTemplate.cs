using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models;

namespace AccountingSystem.Models.CompoundJournals
{
    public class CompoundJournalTemplate
    {
        public string? Description { get; set; }

        public int? BranchId { get; set; }

        public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Posted;

        public List<CompoundJournalCondition>? Conditions { get; set; }

        public List<CompoundJournalLineTemplate> Lines { get; set; } = new();

        public Dictionary<string, string>? DefaultContext { get; set; }
    }

    public class CompoundJournalCondition
    {
        [Required]
        public string ContextKey { get; set; } = string.Empty;

        public CompoundJournalConditionOperator Operator { get; set; } = CompoundJournalConditionOperator.Equals;

        [Required]
        public string Value { get; set; } = string.Empty;
    }

    public enum CompoundJournalConditionOperator
    {
        Equals = 1,
        NotEquals = 2,
        GreaterThan = 3,
        GreaterThanOrEqual = 4,
        LessThan = 5,
        LessThanOrEqual = 6,
        Contains = 7,
        NotContains = 8,
        Exists = 9,
        NotExists = 10
    }

    public class CompoundJournalLineTemplate
    {
        public int AccountId { get; set; }

        public string? Description { get; set; }

        public TemplateValue Debit { get; set; } = TemplateValue.Zero();

        public TemplateValue Credit { get; set; } = TemplateValue.Zero();

        public int? CostCenterId { get; set; }
    }

    public class TemplateValue
    {
        public TemplateValueType Type { get; set; } = TemplateValueType.Fixed;

        public decimal? FixedValue { get; set; }

        public string? ContextKey { get; set; }

        public string? Expression { get; set; }

        public static TemplateValue Zero() => new TemplateValue { Type = TemplateValueType.Fixed, FixedValue = 0m };
    }

    public enum TemplateValueType
    {
        Fixed = 1,
        ContextValue = 2,
        Expression = 3
    }
}
