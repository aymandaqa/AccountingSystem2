using System.Collections.Generic;
using System.Linq;
using AccountingSystem.Models.Reports;

namespace AccountingSystem.ViewModels.Dashboard
{
    public class CashPerformanceDashboardViewModel
    {
        public IReadOnlyList<CashPerformanceRecord> Records { get; set; } = new List<CashPerformanceRecord>();

        public decimal TotalCustomerDuesOnRoad { get; set; }

        public decimal TotalCashWithDriverOnRoad { get; set; }

        public decimal TotalCustomerDues { get; set; }

        public decimal TotalCashOnBranchBox { get; set; }

        public bool HasRecords => Records.Any();
    }
}
