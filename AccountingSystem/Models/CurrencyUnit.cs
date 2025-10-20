using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
    public class CurrencyUnit
    {
        public int Id { get; set; }

        [Required]
        public int CurrencyId { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; } = string.Empty;

        [Column(TypeName = "decimal(18,6)")]
        [Range(typeof(decimal), "0.000001", "79228162514264337593543950335")]
        public decimal ValueInBaseUnit { get; set; }

        public Currency? Currency { get; set; }
    }
}
