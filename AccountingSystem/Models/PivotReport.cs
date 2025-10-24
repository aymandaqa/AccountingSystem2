using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class PivotReport
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public DynamicReportType ReportType { get; set; }

        [Required]
        public string Layout { get; set; } = string.Empty;

        [Required]
        public string CreatedById { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public virtual User CreatedBy { get; set; } = null!;
    }
}
