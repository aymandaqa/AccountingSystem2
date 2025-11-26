using System;
using System.Collections.Generic;

namespace AccountingSystem.ViewModels.Dashboard
{
    public class ProfitabilityReportViewModel
    {
        public DateTime StartDate { get; set; }
        public DateTime EndDate { get; set; }

        public decimal TotalExpenses { get; set; }
        public decimal NetProfit { get; set; }
        public decimal NetProfitChangePercent { get; set; }
        public decimal ExpensesChangePercent { get; set; }
        public bool IsNetProfitPositive => NetProfit >= 0;

        public IReadOnlyList<WeeklyProfitComparison> WeeklyComparisons { get; set; } = Array.Empty<WeeklyProfitComparison>();
        public TargetShipmentSummary ShipmentTarget { get; set; } = new TargetShipmentSummary();
        public IReadOnlyList<TopContributor> TopDrivers { get; set; } = Array.Empty<TopContributor>();
        public IReadOnlyList<TopContributor> TopUsers { get; set; } = Array.Empty<TopContributor>();
        public IReadOnlyList<BranchTargetSummary> BranchTargets { get; set; } = Array.Empty<BranchTargetSummary>();
        public AnnualProfitProjection AnnualProjection { get; set; } = new AnnualProfitProjection();
    }

    public class WeeklyProfitComparison
    {
        public string Label { get; set; } = string.Empty;
        public decimal Expenses { get; set; }
        public decimal NetProfit { get; set; }
        public decimal ProfitChangePercent { get; set; }
    }

    public class TargetShipmentSummary
    {
        public int TargetShipments { get; set; }
        public decimal BreakEvenRevenue { get; set; }
        public decimal AverageRevenuePerShipment { get; set; }
    }

    public class TopContributor
    {
        public string Name { get; set; } = string.Empty;
        public decimal Value { get; set; }
        public string Descriptor { get; set; } = string.Empty;
    }

    public class BranchTargetSummary
    {
        public string BranchName { get; set; } = string.Empty;
        public decimal RequiredTarget { get; set; }
        public decimal CurrentCoverage { get; set; }
    }

    public class AnnualProfitProjection
    {
        public decimal ProjectedNetProfit { get; set; }
        public decimal AnnualizedRevenue { get; set; }
        public decimal AnnualizedExpenses { get; set; }
    }
}
