using System;
namespace AccountingSystem.ViewModels
{
    public class CreateCurrencyViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; } = 1m;
        public bool IsBase { get; set; } = false;
    }
    public class EditCurrencyViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; } = 1m;
        public bool IsBase { get; set; } = false;
    }
    public class CurrencyViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; } = 1m;
        public bool IsBase { get; set; } = false;
    }
}
