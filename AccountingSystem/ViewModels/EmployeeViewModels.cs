using System;
using System.ComponentModel.DataAnnotations;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class EmployeeListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public string? JobTitle { get; set; }
        public decimal Salary { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public int? AccountId { get; set; }
        public decimal AccountBalance { get; set; }
        public bool IsActive { get; set; }
        public string? NationalId { get; set; }
    }

    public abstract class EmployeeFormViewModel
    {
        [Required]
        [StringLength(200)]
        [Display(Name = "اسم الموظف")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [StringLength(50)]
        [Display(Name = "رقم الهاتف")]
        public string? PhoneNumber { get; set; }

        [StringLength(50)]
        [Display(Name = "رقم الهوية")]
        public string? NationalId { get; set; }

        [Required]
        [Display(Name = "الفرع")]
        public int BranchId { get; set; }

        [Display(Name = "تاريخ التعيين")]
        [DataType(DataType.Date)]
        public DateTime HireDate { get; set; } = DateTime.Today;

        [Display(Name = "الراتب")]
        [Range(0, double.MaxValue, ErrorMessage = "الراتب غير صحيح")]
        [DataType(DataType.Currency)]
        public decimal Salary { get; set; }

        [Display(Name = "المسمى الوظيفي")]
        [StringLength(200)]
        public string? JobTitle { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        public IEnumerable<SelectListItem> Branches { get; set; } = Enumerable.Empty<SelectListItem>();
    }

    public class CreateEmployeeViewModel : EmployeeFormViewModel
    {
    }

    public class EditEmployeeViewModel : EmployeeFormViewModel
    {
        public int Id { get; set; }
        public string AccountCode { get; set; } = string.Empty;
    }
}
