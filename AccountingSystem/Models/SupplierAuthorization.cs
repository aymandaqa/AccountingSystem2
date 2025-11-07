using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    [System.Flags]
    public enum SupplierAuthorization
    {
        [Display(Name = "بدون")]
        None = 0,

        [Display(Name = "سند الدفع")]
        PaymentVoucher = 1,

        [Display(Name = "سند القبض")]
        ReceiptVoucher = 2,

        [Display(Name = "شاشة الإدخال الديناميكي")]
        DynamicScreenEntry = 4,

        [Display(Name = "سند المصاريف")]
        DisbursementVoucher = 8,

        [Display(Name = "مصروفات الأصل الثابت")]
        AssetExpense = 16
    }
}
