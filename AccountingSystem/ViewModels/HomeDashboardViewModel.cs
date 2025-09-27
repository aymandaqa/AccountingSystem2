using System;
using System.Collections.Generic;

namespace AccountingSystem.ViewModels
{
    public class MonthlyFinancialData
    {
        public string Month { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal Expenses { get; set; }
        public decimal Profit { get; set; }
    }

    public class BranchPerformanceData
    {
        public string Department { get; set; } = string.Empty;
        public decimal Score { get; set; }
    }

    public class MarketShareData
    {
        public string Company { get; set; } = string.Empty;
        public decimal Share { get; set; }
    }

    public class IncomeSourceData
    {
        public string Source { get; set; } = string.Empty;
        public decimal Value { get; set; }
    }

    public class SalesScatterPoint
    {
        public decimal Price { get; set; }
        public decimal Units { get; set; }
    }

    public class RiskReturnPoint
    {
        public string Sector { get; set; } = string.Empty;
        public decimal Risk { get; set; }
        public decimal Return { get; set; }
        public decimal Size { get; set; }
    }

    public class BalancedScorecardMetric
    {
        public string Dimension { get; set; } = string.Empty;
        public decimal Score { get; set; }
    }

    public class HomeDashboardViewModel
    {
        public DateTime FromDate { get; set; }
        public DateTime ToDate { get; set; }
        public List<MonthlyFinancialData> MonthlyFinancials { get; set; } = new();
        public List<BranchPerformanceData> DepartmentPerformance { get; set; } = new();
        public List<MarketShareData> MarketShare { get; set; } = new();
        public List<IncomeSourceData> IncomeSources { get; set; } = new();
        public List<SalesScatterPoint> SalesScatter { get; set; } = new();
        public List<RiskReturnPoint> RiskReturn { get; set; } = new();
        public List<BalancedScorecardMetric> BalancedScorecard { get; set; } = new();
    }
}
