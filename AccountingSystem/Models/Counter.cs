using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class Counter
    {
        public int Id { get; set; }

        [Required]
        [MaxLength(100)]
        public string Key { get; set; } = string.Empty;

        public int Year { get; set; }

        public long Value { get; set; }
    }
}
