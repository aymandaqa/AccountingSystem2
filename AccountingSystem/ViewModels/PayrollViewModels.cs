using System;
using System.Collections.Generic;

namespace AccountingSystem.ViewModels
{
    public class PayrollEmployeeViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string BranchName { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public decimal Salary { get; set; }
        public string? JobTitle { get; set; }
        public bool IsActive { get; set; }
        public List<PayrollEmployeeDeductionSelection> Deductions { get; set; } = new();
        public List<PayrollEmployeeAllowanceSelection> Allowances { get; set; } = new();
    }

    public class PayrollBatchSummaryViewModel
    {
        public int BatchId { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalGrossAmount { get; set; }
        public decimal TotalDeductionAmount { get; set; }
        public decimal TotalAllowanceAmount { get; set; }
        public decimal TotalBaseAmount { get; set; }
        public int EmployeeCount { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public List<PayrollBranchSummaryViewModel> Branches { get; set; } = new();
    }

    public class PayrollBranchSummaryViewModel
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int EmployeeCount { get; set; }
        public decimal TotalAmount { get; set; }
        public decimal TotalGrossAmount { get; set; }
        public decimal TotalDeductionAmount { get; set; }
        public decimal TotalAllowanceAmount { get; set; }
        public decimal TotalBaseAmount { get; set; }
    }

    public class PayrollBatchHistoryViewModel
    {
        public int Id { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string PeriodName { get; set; } = string.Empty;
        public int Year { get; set; }
        public int Month { get; set; }
        public decimal TotalAmount { get; set; }
        public int EmployeeCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string? ReferenceNumber { get; set; }
    }

    public class PayrollBatchPrintViewModel
    {
        public int BatchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string PeriodDisplay { get; set; } = string.Empty;
        public string StatusDisplay { get; set; } = string.Empty;
        public string? ReferenceNumber { get; set; }
        public string? PaymentAccount { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public decimal TotalNet { get; set; }
        public decimal TotalBase { get; set; }
        public decimal TotalAllowance { get; set; }
        public decimal TotalDeduction { get; set; }
        public List<PayrollBatchEmployeePrintViewModel> Employees { get; set; } = new();
    }

    public class PayrollBatchEmployeePrintViewModel
    {
        public int EmployeeId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string JobTitle { get; set; } = string.Empty;
        public decimal BaseSalary { get; set; }
        public decimal AllowanceTotal { get; set; }
        public decimal DeductionTotal { get; set; }
        public decimal NetAmount { get; set; }
        public List<PayrollItemBreakdownViewModel> Allowances { get; set; } = new();
        public List<PayrollItemBreakdownViewModel> Deductions { get; set; } = new();
    }

    public class PayrollItemBreakdownViewModel
    {
        public string Type { get; set; } = string.Empty;
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string? AccountCode { get; set; }
    }

    public class PayrollEmployeeSelection
    {
        public int EmployeeId { get; set; }
        public List<PayrollEmployeeDeductionSelection> Deductions { get; set; } = new();
        public List<PayrollEmployeeAllowanceSelection> Allowances { get; set; } = new();
        public bool HasAllowanceSelection { get; set; }
    }

    public class PayrollEmployeeDeductionSelection
    {
        public int? DeductionTypeId { get; set; }
        public string? Type { get; set; }
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string? AccountName { get; set; }
        public string? AccountCode { get; set; }
        public int? AccountId { get; set; }
        public int? EmployeeLoanInstallmentId { get; set; }
    }

    public class PayrollEmployeeAllowanceSelection
    {
        public int? AllowanceTypeId { get; set; }
        public string? Type { get; set; }
        public string? Description { get; set; }
        public decimal Amount { get; set; }
        public string? AccountName { get; set; }
        public string? AccountCode { get; set; }
    }

    public class CreatePayrollBatchRequest
    {
        public int BranchId { get; set; }
        public int Year { get; set; }
        public int Month { get; set; }
        public List<PayrollEmployeeSelection> Employees { get; set; } = new();
    }

    public class ConfirmPayrollBatchRequest
    {
        public int BatchId { get; set; }
    }

    public class PayrollMonthOptionViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string Name { get; set; } = string.Empty;
    }
}
