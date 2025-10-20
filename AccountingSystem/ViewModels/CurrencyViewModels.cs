using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.ViewModels
{
    public class CurrencyUnitInputModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "الرجاء إدخال اسم الوحدة")]
        [StringLength(100, ErrorMessage = "اسم الوحدة يجب ألا يتجاوز 100 حرف")]
        public string Name { get; set; } = string.Empty;

        [Required(ErrorMessage = "الرجاء إدخال قيمة الوحدة بالنسبة للوحدة الأساسية")]
        [Range(typeof(decimal), "0.000001", "79228162514264337593543950335", ErrorMessage = "القيمة يجب أن تكون أكبر من صفر")]
        public decimal ValueInBaseUnit { get; set; } = 1m;
    }

    public class CreateCurrencyViewModel
    {
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; } = 1m;
        public bool IsBase { get; set; } = false;

        public IList<CurrencyUnitInputModel> Units { get; set; } = new List<CurrencyUnitInputModel>
        {
            new CurrencyUnitInputModel
            {
                Name = string.Empty,
                ValueInBaseUnit = 1m
            }
        };
    }
    public class EditCurrencyViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; } = 1m;
        public bool IsBase { get; set; } = false;

        public IList<CurrencyUnitInputModel> Units { get; set; } = new List<CurrencyUnitInputModel>();
    }
    public class CurrencyViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Code { get; set; } = string.Empty;
        public decimal ExchangeRate { get; set; } = 1m;
        public bool IsBase { get; set; } = false;

        public IList<CurrencyUnitInputModel> Units { get; set; } = new List<CurrencyUnitInputModel>();
    }
}
