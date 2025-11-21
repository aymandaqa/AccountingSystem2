using System.Collections.Generic;
using System.Linq;
using AccountingSystem.Models.Reports;
using AccountingSystem.ViewModels;
using AccountingSystem.ViewModels.Workflows;
using AccountingSystem.Models;

namespace AccountingSystem.ViewModels.Dashboard
{
    public class CashPerformanceDashboardViewModel
    {
        public IReadOnlyList<CashPerformanceRecord> Records { get; set; } = new List<CashPerformanceRecord>();

        public IReadOnlyList<PendingWorkflowRequestViewModel> MyPendingRequests { get; set; } = new List<PendingWorkflowRequestViewModel>();

        public IReadOnlyList<WorkflowApprovalViewModel> PendingApprovals { get; set; } = new List<WorkflowApprovalViewModel>();

        public IReadOnlyList<PaymentTransfer> PendingTransfers { get; set; } = new List<PaymentTransfer>();

        public IReadOnlyList<PaymentTransfer> PendingIncomingTransfers { get; set; } = new List<PaymentTransfer>();

        public decimal TotalCustomerDuesOnRoad { get; set; }

        public decimal TotalCashWithDriverOnRoad { get; set; }

        public decimal TotalCustomerDues { get; set; }

        public decimal TotalCashOnBranchBox { get; set; }

        public decimal TransfersBalance { get; set; }

        public IReadOnlyList<AccountTreeNodeViewModel> DashboardAccountTree { get; set; } = new List<AccountTreeNodeViewModel>();

        public string DashboardBaseCurrencyCode { get; set; } = string.Empty;

        public string DashboardParentAccountName { get; set; } = string.Empty;

        public bool HasRecords => Records.Any();

        public bool HasPendingApprovals => PendingApprovals.Any();

        public bool HasDashboardAccounts => DashboardAccountTree.Any();

        public bool HasPendingTransfers => PendingTransfers.Any();

        public bool HasPendingIncomingTransfers => PendingIncomingTransfers.Any();

        public string CurrentUserId { get; set; } = string.Empty;
    }
}
