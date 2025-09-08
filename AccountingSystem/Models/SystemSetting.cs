using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class SystemSetting
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Key { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Value { get; set; }
    }
}
