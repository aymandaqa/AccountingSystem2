using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
    public class ReceiptVoucher
    {
        public int Id { get; set; }

        [Required]
        public int AccountId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal ExchangeRate { get; set; } = 1m;

        public DateTime Date { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Notes { get; set; }

        public virtual Account Account { get; set; } = null!;
    }
}
