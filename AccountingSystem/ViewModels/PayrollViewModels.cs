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
    }

    public class PayrollBatchSummaryViewModel
    {
        public int BatchId { get; set; }
        public decimal TotalAmount { get; set; }
        public int EmployeeCount { get; set; }
        public List<PayrollBranchSummaryViewModel> Branches { get; set; } = new();
    }

    public class PayrollBranchSummaryViewModel
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public int EmployeeCount { get; set; }
        public decimal TotalAmount { get; set; }
    }

    public class PayrollBatchHistoryViewModel
    {
        public int Id { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public string PaymentAccountName { get; set; } = string.Empty;
        public decimal TotalAmount { get; set; }
        public int EmployeeCount { get; set; }
        public string Status { get; set; } = string.Empty;
        public DateTime CreatedAt { get; set; }
        public DateTime? ConfirmedAt { get; set; }
        public string? ReferenceNumber { get; set; }
    }

    public class CreatePayrollBatchRequest
    {
        public int BranchId { get; set; }
        public int PaymentAccountId { get; set; }
        public List<int> EmployeeIds { get; set; } = new();
    }

    public class ConfirmPayrollBatchRequest
    {
        public int BatchId { get; set; }
    }
}
