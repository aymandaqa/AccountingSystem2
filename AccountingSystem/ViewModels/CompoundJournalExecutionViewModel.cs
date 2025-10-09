using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models;

namespace AccountingSystem.ViewModels
{
    public class CompoundJournalExecutionViewModel
    {
        public int DefinitionId { get; set; }

        public string DefinitionName { get; set; } = string.Empty;

        [Display(Name = "تاريخ التنفيذ")]
        public DateTime ExecutionDate { get; set; } = DateTime.UtcNow;

        [Display(Name = "تاريخ القيد")]
        [DataType(DataType.Date)]
        public DateTime? JournalDate { get; set; }

        [Display(Name = "الوصف")]
        public string? DescriptionOverride { get; set; }

        [Display(Name = "المرجع")]
        public string? ReferenceOverride { get; set; }

        [Display(Name = "الفرع")]
        public int? BranchIdOverride { get; set; }

        [Display(Name = "الحالة")]
        public JournalEntryStatus? StatusOverride { get; set; }

        [Display(Name = "قيمة السياق (JSON)")]
        public string? ContextJson { get; set; }

        public Dictionary<int, string> Branches { get; set; } = new();
    }
}
