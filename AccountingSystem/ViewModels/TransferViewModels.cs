using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Http;
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

        [Display(Name = "المرفق (اختياري)")]
        public IFormFile? Attachment { get; set; }

        public IEnumerable<SelectListItem> Receivers { get; set; } = new List<SelectListItem>();

        public IEnumerable<SenderAccountOption> SenderAccounts { get; set; } = new List<SenderAccountOption>();

        public Dictionary<string, string> ReceiverBranches { get; set; } = new();

        public Dictionary<string, List<ReceiverAccountOption>> ReceiverAccounts { get; set; } = new();

        public Dictionary<int, List<CurrencyUnitOption>> CurrencyUnits { get; set; } = new();

        public List<CurrencyUnitCountInput> CurrencyUnitCounts { get; set; } = new();

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

        public class CurrencyUnitOption
        {
            public int CurrencyUnitId { get; set; }
            public string Name { get; set; } = string.Empty;
            public decimal ValueInBaseUnit { get; set; }
        }

        public class CurrencyUnitCountInput
        {
            public int CurrencyUnitId { get; set; }
            public int Count { get; set; }
        }
    }

    public class TransferEditViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الرجاء اختيار المستلم")]
        public string? ReceiverId { get; set; }

        [Display(Name = "المبلغ")]
        [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        public decimal Amount { get; set; }

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public string SenderBranch { get; set; } = string.Empty;

        public IEnumerable<SelectListItem> Receivers { get; set; } = new List<SelectListItem>();

        [Display(Name = "المرفق (اختياري)")]
        public IFormFile? Attachment { get; set; }

        public string? ExistingAttachmentPath { get; set; }

        public string? ExistingAttachmentName { get; set; }

        [Display(Name = "إزالة المرفق الحالي")]
        public bool RemoveAttachment { get; set; }

        public Dictionary<string, string> ReceiverBranches { get; set; } = new();

        public Dictionary<string, List<TransferCreateViewModel.ReceiverAccountOption>> ReceiverAccounts { get; set; } = new();

        public List<TransferCreateViewModel.CurrencyUnitOption> CurrencyUnits { get; set; } = new();

        public List<TransferCreateViewModel.CurrencyUnitCountInput> CurrencyUnitCounts { get; set; } = new();

        public int CurrencyId { get; set; }

        public string CurrencyCode { get; set; } = string.Empty;

        public string? ReturnUrl { get; set; }
    }

    public class TransferPrintViewModel
    {
        public PaymentTransfer Transfer { get; set; } = null!;

        public Dictionary<int, int> CurrencyBreakdown { get; set; } = new();

        public Dictionary<int, string> CurrencyUnitNames { get; set; } = new();

        public Account? IntermediaryAccount { get; set; }

        public string? ReturnUrl { get; set; }
    }

    public class TransferManagementFilters
    {
        [Display(Name = "بحث في الجميع")]
        public string? SearchTerm { get; set; }

        [Display(Name = "فرع الإرسال")]
        public int? FromBranchId { get; set; }

        [Display(Name = "فرع الاستقبال")]
        public int? ToBranchId { get; set; }

        [Display(Name = "من تاريخ")]
        [DataType(DataType.Date)]
        public DateTime? FromDate { get; set; }

        [Display(Name = "إلى تاريخ")]
        [DataType(DataType.Date)]
        public DateTime? ToDate { get; set; }

        public bool HasFilters =>
            !string.IsNullOrWhiteSpace(SearchTerm) ||
            FromBranchId.HasValue ||
            ToBranchId.HasValue ||
            FromDate.HasValue ||
            ToDate.HasValue;

        public void Normalize()
        {
            SearchTerm = string.IsNullOrWhiteSpace(SearchTerm) ? null : SearchTerm.Trim();

            if (FromDate.HasValue)
                FromDate = FromDate.Value.Date;

            if (ToDate.HasValue)
                ToDate = ToDate.Value.Date;
        }
    }

    public class TransferManagementViewModel
    {
        public TransferManagementFilters Filters { get; set; } = new();

        public List<PaymentTransfer> Transfers { get; set; } = new();

        public IEnumerable<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
    }
}
