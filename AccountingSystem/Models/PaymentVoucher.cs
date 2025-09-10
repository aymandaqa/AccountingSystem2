using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
    public class PaymentVoucher
    {
        public int Id { get; set; }

        [Required]
        public int SupplierId { get; set; }

        // Account used when voucher is non-cash (credit account)
        public int? AccountId { get; set; }

        [Required]
        public int CurrencyId { get; set; }

        [Column(TypeName = "decimal(18,2)")]
        public decimal Amount { get; set; }

        [Column(TypeName = "decimal(18,6)")]
        public decimal ExchangeRate { get; set; } = 1m;

        public DateTime Date { get; set; } = DateTime.Now;

        [StringLength(500)]
        public string? Notes { get; set; }

        [Required]
        public string CreatedById { get; set; } = string.Empty;

        public bool IsCash { get; set; }

        public virtual Supplier Supplier { get; set; } = null!;
        public virtual Account? Account { get; set; }
        public virtual Currency Currency { get; set; } = null!;
        public virtual User CreatedBy { get; set; } = null!;
    }
}

