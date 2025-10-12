using System;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.ViewModels
{
    public class EmployeeOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public decimal Salary { get; set; }
        public decimal AccountBalance { get; set; }
        public decimal AccountAvailableBalance { get; set; }
        public decimal DailySalaryRate { get; set; }
        public decimal AccruedSalaryBalance { get; set; }
        public decimal MaxAdvanceAmount { get; set; }
    }

    public class SalaryPaymentCreateViewModel
    {
        [Display(Name = "الموظف")]
        [Required(ErrorMessage = "الرجاء اختيار الموظف")]
        public int EmployeeId { get; set; }

        [Display(Name = "المبلغ")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ غير صالح")]
        public decimal Amount { get; set; }

        [Display(Name = "التاريخ")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        public List<EmployeeOptionViewModel> Employees { get; set; } = new();

        public string PaymentAccountName { get; set; } = string.Empty;

        public decimal PaymentAccountBalance { get; set; }

        public string CurrencyCode { get; set; } = string.Empty;
    }

    public class EmployeeAdvanceCreateViewModel
    {
        [Display(Name = "الموظف")]
        [Required(ErrorMessage = "الرجاء اختيار الموظف")]
        public int EmployeeId { get; set; }

        [Display(Name = "المبلغ")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ غير صالح")]
        public decimal Amount { get; set; }

        [Display(Name = "التاريخ")]
        [DataType(DataType.Date)]
        public DateTime Date { get; set; } = DateTime.Today;

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        public List<EmployeeOptionViewModel> Employees { get; set; } = new();

        public string PaymentAccountName { get; set; } = string.Empty;

        public decimal PaymentAccountBalance { get; set; }

        public string CurrencyCode { get; set; } = string.Empty;
    }
}
