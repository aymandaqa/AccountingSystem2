using System;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class UserPaymentAccount
    {
        [Required]
        public string UserId { get; set; } = string.Empty;
        public int CurrencyId { get; set; }
        public int AccountId { get; set; }

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Currency Currency { get; set; } = null!;
        public virtual Account Account { get; set; } = null!;
    }
}
