using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    [System.Flags]
    public enum SupplierAuthorization
    {
        [Display(Name = "بدون")]
        None = 0,

        [Display(Name = "الدفع")]
        Payment = 1,

        [Display(Name = "القبض")]
        Receipt = 2
    }
}
