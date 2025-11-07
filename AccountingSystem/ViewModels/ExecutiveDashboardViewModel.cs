using System;
using System.Collections.Generic;

namespace AccountingSystem.ViewModels
{
    public class ExecutiveDashboardMetric
    {
        public string Title { get; set; } = string.Empty;
        public decimal Actual { get; set; }
        public decimal Target { get; set; }
        public string Unit { get; set; } = string.Empty;
        public bool IsPercentage { get; set; }
        public string? Tooltip { get; set; }

        public decimal Variance => Math.Round(Actual - Target, 2, MidpointRounding.AwayFromZero);

        public decimal Achievement
        {
            get
            {
                if (IsPercentage)
                {
                    return Clamp(Actual, 0m, 100m);
                }

                if (Target == 0m)
                {
                    return Actual > 0m ? 100m : 0m;
                }

                var ratio = Target == 0m ? 0m : (Actual / Target) * 100m;
                ratio = Math.Round(ratio, 2, MidpointRounding.AwayFromZero);
                return Clamp(ratio, 0m, 200m);
            }
        }

        private static decimal Clamp(decimal value, decimal min, decimal max)
        {
            if (value < min)
            {
                return min;
            }

            if (value > max)
            {
                return max;
            }

            return value;
        }
    }

    public class ExecutiveDashboardTrendPoint
    {
        public int MonthNumber { get; set; }
        public string Label { get; set; } = string.Empty;
        public decimal Revenue { get; set; }
        public decimal CostOfSales { get; set; }
        public decimal OperatingExpenses { get; set; }
        public decimal Profit { get; set; }
        public bool IsSelected { get; set; }
    }

    public class CashConversionMetric
    {
        public string Name { get; set; } = string.Empty;
        public decimal MonthlyValue { get; set; }
        public decimal YearToDateValue { get; set; }
    }

    public class OperatingExpenseBreakdownItem
    {
        public string Name { get; set; } = string.Empty;
        public decimal MonthlyAmount { get; set; }
        public decimal YearToDateAmount { get; set; }
    }

    public class ExecutiveIncomeStatementRow
    {
        public string Name { get; set; } = string.Empty;
        public decimal MonthlyActual { get; set; }
        public decimal MonthlyTarget { get; set; }
        public decimal YearToDateActual { get; set; }
        public decimal YearToDateTarget { get; set; }
    }

    public class ExecutiveDashboardViewModel
    {
        public int Year { get; set; }
        public int Month { get; set; }
        public string MonthDisplay { get; set; } = string.Empty;
        public string YearDisplay { get; set; } = string.Empty;
        public string CurrencyCode { get; set; } = string.Empty;
        public List<int> AvailableYears { get; set; } = new();
        public List<int> AvailableMonths { get; set; } = new();
        public List<ExecutiveDashboardMetric> MonthlyMetrics { get; set; } = new();
        public List<ExecutiveDashboardMetric> YearToDateMetrics { get; set; } = new();
        public List<ExecutiveDashboardTrendPoint> MonthlyTrend { get; set; } = new();
        public List<CashConversionMetric> CashConversionMetrics { get; set; } = new();
        public decimal CashConversionCycleMonthly { get; set; }
        public decimal CashConversionCycleYearToDate { get; set; }
        public List<OperatingExpenseBreakdownItem> OperatingExpenseBreakdown { get; set; } = new();
        public List<ExecutiveIncomeStatementRow> IncomeStatement { get; set; } = new();
        public List<CustomerBranchAccountNode> CustomerAccountBranches { get; set; } = new();
    }

    public class CustomerBranchAccountNode
    {
        public int? BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public decimal TotalBalanceBase { get; set; }
        public List<CustomerAccountBalanceNode> Customers { get; set; } = new();
        public bool HasCustomers => Customers.Count > 0;
    }

    public class CustomerAccountBalanceNode
    {
        public string CustomerId { get; set; } = string.Empty;
        public string CustomerName { get; set; } = string.Empty;
        public string? CustomerContact { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public decimal BalanceBase { get; set; }
        public decimal BalanceOriginal { get; set; }
        public string AccountCurrencyCode { get; set; } = string.Empty;
    }
}
