using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Linq;

namespace AccountingSystem.ViewModels
{
    public class AssetListViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string Type { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string? AssetNumber { get; set; }
        public string? Notes { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
    }

    public class AssetFormViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "اسم الأصل")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        [Display(Name = "نوع الأصل")]
        public string Type { get; set; } = string.Empty;

        [Required]
        [Display(Name = "الفرع")]
        public int BranchId { get; set; }

        [StringLength(100)]
        [Display(Name = "رقم الأصل")]
        public string? AssetNumber { get; set; }

        [StringLength(500)]
        [Display(Name = "ملاحظات")]
        public string? Notes { get; set; }

        public IEnumerable<SelectListItem> Branches { get; set; } = Enumerable.Empty<SelectListItem>();
    }

    public class AssetExpenseListViewModel
    {
        public int Id { get; set; }
        public string AssetName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string ExpenseAccountName { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public bool IsCash { get; set; }
        public DateTime Date { get; set; }
        public string? Notes { get; set; }
    }

    public class CreateAssetExpenseViewModel
    {
        [Required]
        [Display(Name = "الأصل")]
        public int AssetId { get; set; }

        [Required]
        [Display(Name = "الحساب")]
        public int ExpenseAccountId { get; set; }

        [Display(Name = "حساب التسوية")]
        public int? AccountId { get; set; }

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
        public IEnumerable<AssetExpenseAccountOption> Accounts { get; set; } = Enumerable.Empty<AssetExpenseAccountOption>();
    }

    public class AssetExpenseAccountOption
    {
        public int Id { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public int CurrencyId { get; set; }
        public string CurrencyCode { get; set; } = string.Empty;
    }
}
