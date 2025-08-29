using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class CostCenter
    {
        public int Id { get; set; }

        [Required]
        [StringLength(10)]
        public string Code { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string NameAr { get; set; } = string.Empty;

        [StringLength(200)]
        public string? NameEn { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public int? ParentId { get; set; }

        public bool IsActive { get; set; } = true;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? UpdatedAt { get; set; }

        // Navigation properties
        public virtual CostCenter? Parent { get; set; }
        public virtual ICollection<CostCenter> Children { get; set; } = new List<CostCenter>();
        public virtual ICollection<JournalEntryLine> JournalEntryLines { get; set; } = new List<JournalEntryLine>();
    }
}

