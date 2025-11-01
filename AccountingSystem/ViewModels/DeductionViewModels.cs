using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class DeductionTypeListItemViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string AccountDisplay { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; }
    }

    public class DeductionTypeFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "اسم نوع الخصم مطلوب")]
        [StringLength(200, ErrorMessage = "يجب ألا يتجاوز الاسم 200 حرف")]
        [Display(Name = "اسم نوع الخصم")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "يجب ألا يتجاوز الوصف 500 حرف")]
        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "يرجى اختيار حساب الخصم")]
        [Display(Name = "حساب الخصم")]
        public int? AccountId { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        public List<SelectListItem> Accounts { get; set; } = new();
    }

    public class EmployeeDeductionListItemViewModel
    {
        public int Id { get; set; }

        public string EmployeeName { get; set; } = string.Empty;

        public string? EmployeeBranch { get; set; }

        public string DeductionTypeName { get; set; } = string.Empty;

        public string AccountDisplay { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; }

        public int Year { get; set; }

        public int Month { get; set; }

        public string PeriodName { get; set; } = string.Empty;
    }

    public class EmployeeDeductionFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "يرجى اختيار الموظف")]
        [Display(Name = "الموظف")]
        public int? EmployeeId { get; set; }

        [Required(ErrorMessage = "يرجى اختيار نوع الخصم")]
        [Display(Name = "نوع الخصم")]
        public int? DeductionTypeId { get; set; }

        [Required(ErrorMessage = "يرجى إدخال مبلغ الخصم")]
        [Range(0.01, double.MaxValue, ErrorMessage = "يجب أن يكون مبلغ الخصم أكبر من صفر")]
        [Display(Name = "المبلغ")]
        public decimal Amount { get; set; }

        [StringLength(250, ErrorMessage = "يجب ألا يتجاوز الوصف 250 حرف")]
        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        [Required(ErrorMessage = "يرجى اختيار شهر الخصم")]
        [Display(Name = "شهر الخصم")]
        public string? Period { get; set; }

        public List<SelectListItem> Employees { get; set; } = new();

        public List<SelectListItem> DeductionTypes { get; set; } = new();
    }
}
