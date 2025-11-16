using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public enum SupplierType
    {
        [Display(Name = "غير محدد")]
        Unspecified = 0,

        [Display(Name = "شركة")]
        Company = 1,

        [Display(Name = "فرد")]
        Individual = 2,

        [Display(Name = "مورد خارجي")]
        External = 3
    }
}
