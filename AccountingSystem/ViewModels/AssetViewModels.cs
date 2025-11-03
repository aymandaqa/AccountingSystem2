using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AccountingSystem.Models.Workflows;
using AccountingSystem.Models;
using System.Linq;

namespace AccountingSystem.ViewModels
{
    public class AssetListViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AssetTypeName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string? AssetNumber { get; set; }
        public string? Notes { get; set; }
        public decimal OpeningBalance { get; set; }
        public int? AccountId { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public bool IsDepreciable { get; set; }
        public decimal AccumulatedDepreciation { get; set; }
        public decimal BookValue { get; set; }
    }

    public class AssetFormViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "اسم الأصل")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "نوع الأصل")]
        [Range(1, int.MaxValue, ErrorMessage = "اختر نوع الأصل")]
        public int AssetTypeId { get; set; }

        [Required]
        [Display(Name = "الفرع")]
        public int BranchId { get; set; }

        [StringLength(100)]
        [Display(Name = "رقم الأصل")]
        public string? AssetNumber { get; set; }

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        [Display(Name = "الرصيد الافتتاحي")]
        [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "قيمة غير صالحة")]
        public decimal OpeningBalance { get; set; }

        [Required]
        [Display(Name = "حساب رأس المال")]
        public int CapitalAccountId { get; set; }

        public int? AccountId { get; set; }

        [Display(Name = "حساب الأصل")]
        public string? AccountCode { get; set; }

        [Display(Name = "قيمة الأصل")]
        [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "قيمة غير صالحة")]
        public decimal? OriginalCost { get; set; }

        [Display(Name = "قيمة الخردة")]
        [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "قيمة غير صالحة")]
        public decimal? SalvageValue { get; set; }

        [Display(Name = "العمر الافتراضي")]
        [Range(1, int.MaxValue, ErrorMessage = "قيمة غير صالحة")]
        public int? DepreciationPeriods { get; set; }

        [Display(Name = "دورية الإهلاك")]
        public DepreciationFrequency? DepreciationFrequency { get; set; }

        [Display(Name = "تاريخ الشراء")]
        [DataType(DataType.Date)]
        public DateTime? PurchaseDate { get; set; }

        public IEnumerable<SelectListItem> Branches { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> CapitalAccounts { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> AssetTypes { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> DepreciationFrequencies { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<AssetTypeSelectOption> AssetTypeOptions { get; set; } = Enumerable.Empty<AssetTypeSelectOption>();
    }

    public class AssetExpenseListViewModel
    {
        public int Id { get; set; }
        public string AssetName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string ExpenseAccountName { get; set; } = string.Empty;
        public string SupplierName { get; set; } = string.Empty;
        public string? CreatedByName { get; set; }
        public string? ApprovedByName { get; set; }
        public decimal Amount { get; set; }
        public bool IsCash { get; set; }
        public DateTime Date { get; set; }
        public string? Notes { get; set; }
        public int? JournalEntryId { get; set; }
        public string? JournalEntryNumber { get; set; }
        public WorkflowInstanceStatus? WorkflowStatus { get; set; }
    }

    public class CreateAssetExpenseViewModel
    {
        [Required]
        [Display(Name = "الأصل")]
        public int AssetId { get; set; }

        [Required]
        [Display(Name = "الحساب")]
        public int ExpenseAccountId { get; set; }

        [Required]
        [Display(Name = "المورد")]
        public int? SupplierId { get; set; }

        [Display(Name = "العملة")]
        public int CurrencyId { get; set; }

        [Display(Name = "نوع الدفع")]
        public bool IsCash { get; set; } = true;

        [Required]
        [Display(Name = "المبلغ")]
        [Range(typeof(decimal), "0.01", "79228162514264337593543950335", ErrorMessage = "قيمة غير صالحة")]
        public decimal Amount { get; set; }

        [Display(Name = "سعر الصرف")]
        public decimal ExchangeRate { get; set; } = 1m;

        [Display(Name = "التاريخ")]
        public DateTime Date { get; set; } = DateTime.Now;

        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public IEnumerable<SelectListItem> Assets { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<AssetExpenseAccountOption> ExpenseAccounts { get; set; } = Enumerable.Empty<AssetExpenseAccountOption>();
        public IEnumerable<AssetExpenseSupplierOption> Suppliers { get; set; } = Enumerable.Empty<AssetExpenseSupplierOption>();
    }

    public class AssetExpenseAccountOption
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
    }

    public class AssetExpenseSupplierOption
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int AccountId { get; set; }
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
    }

    public class AssetTypeListViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public bool IsDepreciable { get; set; }
        public string? DepreciationExpenseAccountName { get; set; }
        public string? AccumulatedDepreciationAccountName { get; set; }
    }

    public class AssetTypeFormViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "اسم نوع الأصل")]
        public string Name { get; set; } = string.Empty;

        public string? AccountCode { get; set; }

        [Display(Name = "قابل للإهلاك")]
        public bool IsDepreciable { get; set; }

        [Display(Name = "حساب مصروف الإهلاك")]
        public int? DepreciationExpenseAccountId { get; set; }

        [Display(Name = "حساب مجمع الإهلاك")]
        public int? AccumulatedDepreciationAccountId { get; set; }

        public IEnumerable<SelectListItem> DepreciationExpenseAccounts { get; set; } = Enumerable.Empty<SelectListItem>();
        public IEnumerable<SelectListItem> AccumulatedDepreciationAccounts { get; set; } = Enumerable.Empty<SelectListItem>();
    }

    public class AssetTypeSelectOption
    {
        public int Id { get; set; }
        public bool IsDepreciable { get; set; }
    }
}
