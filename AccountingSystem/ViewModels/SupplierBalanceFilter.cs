using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.ViewModels
{
    public enum SupplierBalanceFilter
    {
        [Display(Name = "الجميع")]
        All = 0,

        [Display(Name = "رصيد موجب")]
        Positive = 1,

        [Display(Name = "رصيد سالب")]
        Negative = 2,

        [Display(Name = "رصيد صفر")]
        Zero = 3
    }
}
