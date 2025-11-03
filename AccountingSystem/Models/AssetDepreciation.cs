using System;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class AssetDepreciation
    {
        public int Id { get; set; }

        [Required]
        public int AssetId { get; set; }

        [Required]
        public int PeriodNumber { get; set; }

        [Required]
        public DateTime PeriodStart { get; set; }

        [Required]
        public DateTime PeriodEnd { get; set; }

        [Required]
        [Range(typeof(decimal), "0", "79228162514264337593543950335")]
        public decimal Amount { get; set; }

        [Required]
        [Range(typeof(decimal), "0", "79228162514264337593543950335")]
        public decimal AccumulatedDepreciation { get; set; }

        [Required]
        [Range(typeof(decimal), "0", "79228162514264337593543950335")]
        public decimal BookValue { get; set; }

        [Required]
        public int JournalEntryId { get; set; }

        [Required]
        public string CreatedById { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Asset Asset { get; set; } = null!;

        public virtual JournalEntry JournalEntry { get; set; } = null!;

        public virtual User CreatedBy { get; set; } = null!;
    }
}
