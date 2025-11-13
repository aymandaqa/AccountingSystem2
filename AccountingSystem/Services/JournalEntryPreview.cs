using System.Collections.Generic;
using AccountingSystem.Models;

namespace AccountingSystem.Services
{
    public class JournalEntryPreview
    {
        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public List<JournalEntryPreviewLine> Lines { get; set; } = new();
    }

    public class JournalEntryPreviewLine
    {
        public Account Account { get; set; } = null!;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
        public string? Description { get; set; }
        public CostCenter? CostCenter { get; set; }
    }
}
