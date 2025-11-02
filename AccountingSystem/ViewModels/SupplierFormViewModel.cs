using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class SupplierFormViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "الاسم بالعربية")]
        public string NameAr { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "الاسم بالإنجليزية")]
        public string? NameEn { get; set; }

        [StringLength(200)]
        [Display(Name = "الهاتف")]
        public string? Phone { get; set; }

        [EmailAddress]
        [StringLength(200)]
        [Display(Name = "البريد الإلكتروني")]
        public string? Email { get; set; }

        [Display(Name = "نشط")]    
        public bool IsActive { get; set; } = true;

        [Display(Name = "نوع المود")]
        public SupplierMode Mode { get; set; } = SupplierMode.Cash;

        [Display(Name = "الصلاحيات المسموح بها")]
        public List<SupplierAuthorization> SelectedAuthorizations { get; set; } = new();

        public IEnumerable<SelectListItem> ModeOptions { get; set; } = new List<SelectListItem>();

        public IEnumerable<SelectListItem> AuthorizationOptions { get; set; } = new List<SelectListItem>();
    }
}
