using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public enum SupplierMode
    {
        [Display(Name = "نقدي")]
        Cash = 0,

        [Display(Name = "آجل")]
        Credit = 1,

        [Display(Name = "نقدي وآجل")]
        CashAndCredit = 2
    }
}
