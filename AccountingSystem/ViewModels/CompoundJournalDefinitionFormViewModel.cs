using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models.CompoundJournals;

namespace AccountingSystem.ViewModels
{
    public class CompoundJournalDefinitionFormViewModel
    {
        public int? Id { get; set; }

        [Required]
        [MaxLength(200)]
        [Display(Name = "اسم التعريف")]
        public string Name { get; set; } = string.Empty;

        [MaxLength(1000)]
        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Required]
        [Display(Name = "نوع التنفيذ")]
        public CompoundJournalTriggerType TriggerType { get; set; } = CompoundJournalTriggerType.Manual;

        [Display(Name = "نشط؟")]
        public bool IsActive { get; set; } = true;

        [Display(Name = "تاريخ البدء (UTC)")]
        public DateTime? StartDateUtc { get; set; }

        [Display(Name = "تاريخ الانتهاء (UTC)")]
        public DateTime? EndDateUtc { get; set; }

        [Display(Name = "موعد التشغيل القادم (UTC)")]
        public DateTime? NextRunUtc { get; set; }

        [Display(Name = "نوع التكرار")]
        public CompoundJournalRecurrence? Recurrence { get; set; }

        [Range(1, 365)]
        [Display(Name = "فترة التكرار")]
        public int? RecurrenceInterval { get; set; }

        [Required]
        [Display(Name = "قالب التعريف (JSON)")]
        public string TemplateJson { get; set; } = "";
    }
}
