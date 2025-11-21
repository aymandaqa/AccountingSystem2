using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;
using AccountingSystem.Models;

namespace AccountingSystem.ViewModels
{
    public class EmployeeLoanListItemViewModel
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string AccountDisplay { get; set; } = string.Empty;
        public decimal PrincipalAmount { get; set; }
        public decimal InstallmentAmount { get; set; }
        public int InstallmentCount { get; set; }
        public int PendingInstallments { get; set; }
        public decimal OutstandingAmount { get; set; }
        public DateTime? NextDueDate { get; set; }
        public LoanInstallmentFrequency Frequency { get; set; }
        public bool IsActive { get; set; }
    }

    public class EmployeeLoanFormViewModel
    {
        public int? Id { get; set; }

        [Display(Name = "الموظف")]
        [Required]
        public int EmployeeId { get; set; }

        [Display(Name = "الحساب")]
        public int AccountId { get; set; }

        [Display(Name = "قيمة القرض")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        public decimal PrincipalAmount { get; set; }

        [Display(Name = "قيمة القسط")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        public decimal InstallmentAmount { get; set; }

        [Display(Name = "عدد الأقساط")]
        [Range(1, int.MaxValue, ErrorMessage = "عدد الأقساط يجب أن يكون أكبر من صفر")]
        public int InstallmentCount { get; set; }

        [Display(Name = "تاريخ البداية")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Display(Name = "تاريخ النهاية")]
        [DataType(DataType.Date)]
        public DateTime? EndDate { get; set; }

        [Display(Name = "تكرار القسط")]
        [Required]
        public LoanInstallmentFrequency Frequency { get; set; } = LoanInstallmentFrequency.Monthly;

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;

        public List<SelectListItem> Employees { get; set; } = new();
        public List<SelectListItem> Accounts { get; set; } = new();

        [Display(Name = "بناء جدول أقساط مخصص")]
        public bool UseCustomSchedule { get; set; }

        public List<EmployeeLoanInstallmentViewModel> Installments { get; set; } = new();
    }

    public class EmployeeLoanRescheduleViewModel
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public decimal OutstandingAmount { get; set; }
        public int RemainingInstallments { get; set; }
        public LoanInstallmentFrequency CurrentFrequency { get; set; }

        [Display(Name = "تاريخ البداية الجديد")]
        [DataType(DataType.Date)]
        public DateTime StartDate { get; set; } = DateTime.Today;

        [Display(Name = "قيمة القسط الجديد")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        public decimal InstallmentAmount { get; set; }

        [Display(Name = "عدد الأقساط الجديد")]
        [Range(1, int.MaxValue, ErrorMessage = "عدد الأقساط يجب أن يكون أكبر من صفر")]
        public int InstallmentCount { get; set; }

        [Display(Name = "تكرار القسط")]
        public LoanInstallmentFrequency Frequency { get; set; } = LoanInstallmentFrequency.Monthly;

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }

        [Display(Name = "بناء جدول أقساط مخصص")]
        public bool UseCustomSchedule { get; set; }

        public List<EmployeeLoanInstallmentViewModel> Installments { get; set; } = new();
    }

    public class EmployeeLoanInstallmentViewModel
    {
        public int Id { get; set; }
        public DateTime DueDate { get; set; }
        public decimal Amount { get; set; }
        public decimal PaidAmount { get; set; }
        public LoanInstallmentStatus Status { get; set; }
        public DateTime? PaidAt { get; set; }
        public decimal RemainingAmount => Math.Max(0, Math.Round(Amount - PaidAmount, 2, MidpointRounding.AwayFromZero));
    }

    public class EmployeeLoanDetailsViewModel
    {
        public int Id { get; set; }
        public string EmployeeName { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public string AccountDisplay { get; set; } = string.Empty;
        public decimal PrincipalAmount { get; set; }
        public decimal OutstandingAmount { get; set; }
        public decimal InstallmentAmount { get; set; }
        public int PendingInstallments { get; set; }
        public DateTime? StartDate { get; set; }
        public DateTime? EndDate { get; set; }
        public string? Notes { get; set; }
        public LoanInstallmentFrequency Frequency { get; set; }
        public IEnumerable<EmployeeLoanInstallmentViewModel> Installments { get; set; } = Enumerable.Empty<EmployeeLoanInstallmentViewModel>();
        public EmployeeLoanPaymentFormViewModel PaymentForm { get; set; } = new();
        public IEnumerable<EmployeeLoanPaymentHistoryViewModel> Payments { get; set; } = Enumerable.Empty<EmployeeLoanPaymentHistoryViewModel>();
    }

    public class EmployeeLoanPaymentFormViewModel
    {
        [Display(Name = "المبلغ")]
        [Range(0.01, double.MaxValue, ErrorMessage = "المبلغ يجب أن يكون أكبر من صفر")]
        public decimal Amount { get; set; }

        [Display(Name = "تاريخ السداد")]
        [DataType(DataType.Date)]
        public DateTime PaymentDate { get; set; } = DateTime.Today;

        [Display(Name = "سداد كامل المتبقي")]
        public bool PayFullOutstanding { get; set; }

        [Display(Name = "ملاحظات")]
        [StringLength(500)]
        public string? Notes { get; set; }
    }

    public class EmployeeLoanPaymentHistoryViewModel
    {
        public DateTime PaymentDate { get; set; }
        public decimal Amount { get; set; }
        public string? Notes { get; set; }
        public int? JournalEntryId { get; set; }
    }
}
