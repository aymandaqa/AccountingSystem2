using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class TransferCreateViewModel
    {
        [Required(ErrorMessage = "الرجاء اختيار المستلم")]
        public string? ReceiverId { get; set; }

        [Display(Name = "حساب الإرسال")]
        [Required(ErrorMessage = "الرجاء اختيار حساب الإرسال")]
        public int? FromPaymentAccountId { get; set; }

        [Display(Name = "المبلغ")]
        [Required(ErrorMessage = "الرجاء إدخال مبلغ الحوالة")]
        [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        public decimal Amount { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public string SenderBranch { get; set; } = string.Empty;

        public IEnumerable<SelectListItem> Receivers { get; set; } = new List<SelectListItem>();

        public IEnumerable<SenderAccountOption> SenderAccounts { get; set; } = new List<SenderAccountOption>();

        public Dictionary<string, string> ReceiverBranches { get; set; } = new();

        public Dictionary<string, List<ReceiverAccountOption>> ReceiverAccounts { get; set; } = new();

        public class SenderAccountOption
        {
            public int AccountId { get; set; }
            public string DisplayName { get; set; } = string.Empty;
            public int CurrencyId { get; set; }
            public string CurrencyCode { get; set; } = string.Empty;
        }

        public class ReceiverAccountOption
        {
            public int AccountId { get; set; }
            public int CurrencyId { get; set; }
            public string DisplayName { get; set; } = string.Empty;
        }
    }
}
