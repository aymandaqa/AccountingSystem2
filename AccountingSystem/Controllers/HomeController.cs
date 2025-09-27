using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;

namespace AccountingSystem.Controllers;

public class HomeController : Controller
{
    private readonly ILogger<HomeController> _logger;
    private readonly ApplicationDbContext _context;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context)
    {
        _logger = logger;
        _context = context;
    }

    public async Task<IActionResult> Index(DateTime? fromDate = null, DateTime? toDate = null)
    {
        var startDate = fromDate?.Date ?? new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1).AddMonths(-5);
        var endDate = toDate?.Date ?? DateTime.Today;

        if (endDate < startDate)
        {
            (startDate, endDate) = (endDate, startDate);
        }

        var monthStart = new DateTime(startDate.Year, startDate.Month, 1);
        var monthEnd = new DateTime(endDate.Year, endDate.Month, 1);
        var months = new List<DateTime>();
        for (var month = monthStart; month <= monthEnd; month = month.AddMonths(1))
        {
            months.Add(month);
        }

        if (!months.Any())
        {
            months.Add(monthStart);
        }

        var lineData = await _context.JournalEntryLines
            .Where(l => l.JournalEntry.Date >= startDate && l.JournalEntry.Date <= endDate)
            .Select(l => new
            {
                l.JournalEntryId,
                l.JournalEntry.Date,
                Year = l.JournalEntry.Date.Year,
                Month = l.JournalEntry.Date.Month,
                BranchName = l.JournalEntry.Branch.NameAr,
                l.Account.AccountType,
                AccountName = l.Account.NameAr,
                Debit = l.DebitAmount,
                Credit = l.CreditAmount
            })
            .ToListAsync();

        var monthlyFinancials = months.Select(month =>
        {
            var monthData = lineData.Where(x => x.Year == month.Year && x.Month == month.Month).ToList();
            var revenue = monthData.Where(x => x.AccountType == AccountType.Revenue).Sum(x => x.Credit - x.Debit);
            var expenses = monthData.Where(x => x.AccountType == AccountType.Expenses).Sum(x => x.Debit - x.Credit);
            var profit = revenue - expenses;
            return new MonthlyFinancialData
            {
                Month = month.ToString("yyyy MMM", CultureInfo.GetCultureInfo("ar-SA")),
                MonthDate = month,
                Revenue = Math.Round(revenue, 2, MidpointRounding.AwayFromZero),
                Expenses = Math.Round(expenses, 2, MidpointRounding.AwayFromZero),
                Profit = Math.Round(profit, 2, MidpointRounding.AwayFromZero)
            };
        }).ToList();

        var branchGroups = lineData
            .GroupBy(x => string.IsNullOrWhiteSpace(x.BranchName) ? "غير محدد" : x.BranchName)
            .Select(g => new
            {
                Branch = g.Key,
                Revenue = g.Where(x => x.AccountType == AccountType.Revenue).Sum(x => x.Credit - x.Debit),
                Expenses = g.Where(x => x.AccountType == AccountType.Expenses).Sum(x => x.Debit - x.Credit),
                Entries = g.Select(x => x.JournalEntryId).Distinct().Count()
            })
            .OrderByDescending(g => g.Revenue - g.Expenses)
            .ToList();

        var departmentPerformance = branchGroups
            .Select(g => new BranchPerformanceData
            {
                Department = g.Branch,
                Score = Math.Round(g.Revenue - g.Expenses, 2, MidpointRounding.AwayFromZero)
            })
            .ToList();

        var totalRevenue = branchGroups.Sum(g => g.Revenue);
        var totalExpenses = branchGroups.Sum(g => g.Expenses);
        var netIncome = totalRevenue - totalExpenses;

        var marketShare = totalRevenue > 0
            ? branchGroups.Select(g => new MarketShareData
            {
                Company = g.Branch,
                Share = Math.Round((g.Revenue / totalRevenue) * 100m, 2, MidpointRounding.AwayFromZero)
            }).ToList()
            : new List<MarketShareData>();

        var revenueAccounts = lineData
            .Where(x => x.AccountType == AccountType.Revenue)
            .GroupBy(x => string.IsNullOrWhiteSpace(x.AccountName) ? "غير مسمى" : x.AccountName)
            .Select(g => new
            {
                Account = g.Key,
                Amount = g.Sum(x => x.Credit - x.Debit)
            })
            .Where(g => g.Amount != 0)
            .OrderByDescending(g => g.Amount)
            .ToList();

        var incomeSources = revenueAccounts
            .Take(5)
            .Select(g => new IncomeSourceData
            {
                Source = g.Account,
                Value = Math.Round(g.Amount, 2, MidpointRounding.AwayFromZero)
            })
            .ToList();

        var otherRevenue = revenueAccounts.Skip(5).Sum(g => g.Amount);
        if (otherRevenue > 0)
        {
            incomeSources.Add(new IncomeSourceData
            {
                Source = "أخرى",
                Value = Math.Round(otherRevenue, 2, MidpointRounding.AwayFromZero)
            });
        }

        var receiptVouchers = await _context.ReceiptVouchers
            .Where(v => v.Date >= startDate && v.Date <= endDate)
            .Select(v => new SalesScatterPoint
            {
                Date = v.Date,
                Price = Math.Round(v.Amount, 2, MidpointRounding.AwayFromZero),
                Units = Math.Round(v.ExchangeRate, 4, MidpointRounding.AwayFromZero)
            })
            .ToListAsync();

        var maxEntries = branchGroups.Any() ? branchGroups.Max(g => g.Entries) : 1;
        var riskReturn = branchGroups
            .Select(g => new RiskReturnPoint
            {
                Sector = g.Branch,
                Risk = Math.Round(g.Expenses, 2, MidpointRounding.AwayFromZero),
                Return = Math.Round(g.Revenue, 2, MidpointRounding.AwayFromZero),
                Size = Math.Round(maxEntries == 0 ? 1 : (decimal)g.Entries / maxEntries * 1.5m + 0.5m, 2, MidpointRounding.AwayFromZero)
            })
            .ToList();

        var totalJournalEntries = await _context.JournalEntries
            .Where(e => e.Date >= startDate && e.Date <= endDate)
            .CountAsync();

        var approvedJournalEntries = await _context.JournalEntries
            .Where(e => e.Date >= startDate && e.Date <= endDate && e.Status == JournalEntryStatus.Approved)
            .CountAsync();

        var newAccounts = await _context.Accounts
            .Where(a => a.CreatedAt >= startDate && a.CreatedAt <= endDate)
            .CountAsync();

        decimal ClampScore(decimal value)
        {
            if (value < 0m)
            {
                return 0m;
            }

            return value > 5m ? 5m : Math.Round(value, 2, MidpointRounding.AwayFromZero);
        }

        var financialScore = totalRevenue == 0 ? 0m : ClampScore((netIncome / totalRevenue) * 5m);
        var customerScore = ClampScore(receiptVouchers.Count / 3m);
        var processScore = totalJournalEntries == 0 ? 0m : ClampScore((decimal)approvedJournalEntries / totalJournalEntries * 5m);
        var learningScore = ClampScore(newAccounts / 2m);

        var balancedScorecard = new List<BalancedScorecardMetric>
        {
            new BalancedScorecardMetric { Dimension = "المالية", Score = financialScore },
            new BalancedScorecardMetric { Dimension = "العملاء", Score = customerScore },
            new BalancedScorecardMetric { Dimension = "العمليات الداخلية", Score = processScore },
            new BalancedScorecardMetric { Dimension = "التعلم والنمو", Score = learningScore }
        };

        var viewModel = new HomeDashboardViewModel
        {
            FromDate = startDate,
            ToDate = endDate,
            MonthlyFinancials = monthlyFinancials,
            DepartmentPerformance = departmentPerformance,
            MarketShare = marketShare,
            IncomeSources = incomeSources,
            SalesScatter = receiptVouchers,
            RiskReturn = riskReturn,
            BalancedScorecard = balancedScorecard
        };

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }
}
