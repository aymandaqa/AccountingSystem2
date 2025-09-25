using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Roadfn.Models
{
    public partial class Drife
    {
        public int Id { get; set; }

        [Display(Name = "الاسم الأول")]
        public string? FirstName { get; set; }

        [Display(Name = "الاسم الثاني")]
        public string? SecoundName { get; set; }

        [Display(Name = "اسم العائلة")]
        public string? FamilyName { get; set; }

        [Display(Name = "تاريخ الميلاد")]
        public DateTime? BirthDate { get; set; }

        [Display(Name = "رقم الهاتف 1")]
        public string? Phone1 { get; set; }

        [Display(Name = "رقم الهاتف 2")]
        public string? Phone2 { get; set; }

        [Display(Name = "رقم الهاتف 3")]
        public string? Phone3 { get; set; }

        [Display(Name = "الجنسية")]
        public string? NationalityId { get; set; }

        [Display(Name = "البريد الالكتروني")]
        public string? Email { get; set; }

        [Display(Name = "العنوان")]
        public string? Adress { get; set; }

        [Display(Name = "مالك للسيارة؟")]
        public bool? IsDriverHasOwnCar { get; set; }

        [Display(Name = "الفرع")]
        public int? ActiveBranchId { get; set; }

        [Display(Name = "سيارة السائق ID")]
        public int? DriverCarId { get; set; }

        [Display(Name = "سيارة الشركة ID")]
        public int? CompanyCarId { get; set; }

        [Display(Name = "المنطقة")]
        public string? Area { get; set; }

        [Display(Name = "العمولة لكل طرد")]
        public decimal? CommissionPerItem { get; set; }
        public int? LoginUserId { get; set; }
    }
}
