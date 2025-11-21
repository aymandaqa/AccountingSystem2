using System;

namespace AccountingSystem.ViewModels
{
    public class SupplierGridItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Phone { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string SupplierType { get; set; } = string.Empty;
        public string Permissions { get; set; } = string.Empty;
        public string Branches { get; set; } = string.Empty;
        public string CreatedBy { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public decimal Balance { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
        public int? AccountId { get; set; }
    }
}
