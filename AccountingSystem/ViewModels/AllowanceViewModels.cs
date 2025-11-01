using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class AllowanceTypeListItemViewModel
    {
        public int Id { get; set; }

        public string Name { get; set; } = string.Empty;

        public string AccountDisplay { get; set; } = string.Empty;

        public string? Description { get; set; }

        public bool IsActive { get; set; }
    }

    public class AllowanceTypeFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "اسم نوع البدل مطلوب")]
        [StringLength(200, ErrorMessage = "يجب ألا يتجاوز الاسم 200 حرف")]
        [Display(Name = "اسم نوع البدل")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500, ErrorMessage = "يجب ألا يتجاوز الوصف 500 حرف")]
        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Required(ErrorMessage = "يرجى اختيار حساب البدل")]
        [Display(Name = "حساب البدل")]
        public int? AccountId { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        public List<SelectListItem> Accounts { get; set; } = new();
    }

    public class EmployeeAllowanceListItemViewModel
    {
        public int Id { get; set; }

        public string EmployeeName { get; set; } = string.Empty;

        public string? EmployeeBranch { get; set; }

        public string AllowanceTypeName { get; set; } = string.Empty;

        public string AccountDisplay { get; set; } = string.Empty;

        public decimal Amount { get; set; }

        public string? Description { get; set; }

        public bool IsActive { get; set; }
    }

    public class EmployeeAllowanceFormViewModel
    {
        public int? Id { get; set; }

        [Required(ErrorMessage = "يرجى اختيار الموظف")]
        [Display(Name = "الموظف")]
        public int? EmployeeId { get; set; }

        [Required(ErrorMessage = "يرجى اختيار نوع البدل")]
        [Display(Name = "نوع البدل")]
        public int? AllowanceTypeId { get; set; }

        [Required(ErrorMessage = "يرجى إدخال مبلغ البدل")]
        [Range(0.01, double.MaxValue, ErrorMessage = "يجب أن يكون مبلغ البدل أكبر من صفر")]
        [Display(Name = "المبلغ")]
        public decimal Amount { get; set; }

        [StringLength(250, ErrorMessage = "يجب ألا يتجاوز الوصف 250 حرف")]
        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        public List<SelectListItem> Employees { get; set; } = new();

        public List<SelectListItem> AllowanceTypes { get; set; } = new();
    }
}
