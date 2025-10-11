using System;
using System.Collections.Generic;
using System.Diagnostics;
using System.Globalization;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
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
    private readonly UserManager<User> _userManager;

    public HomeController(ILogger<HomeController> logger, ApplicationDbContext context, UserManager<User> userManager)
    {
        _logger = logger;
        _context = context;
        _userManager = userManager;
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

        var branchFinancials = branchGroups
            .Select(g => new BranchFinancialComparison
            {
                Branch = g.Branch,
                Revenue = Math.Round(g.Revenue, 2, MidpointRounding.AwayFromZero),
                Expenses = Math.Round(g.Expenses, 2, MidpointRounding.AwayFromZero)
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
            BalancedScorecard = balancedScorecard,
            BranchFinancials = branchFinancials
        };

        return View(viewModel);
    }

    public IActionResult Privacy()
    {
        return View();
    }

    [Authorize]
    public async Task<IActionResult> Applications()
    {
        var user = await _userManager.GetUserAsync(User);
        var grantedPermissions = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

        if (user != null)
        {
            var userPermissions = await _context.UserPermissions
                .Where(up => up.UserId == user.Id && up.IsGranted && up.Permission != null)
                .Select(up => up.Permission!.Name)
                .ToListAsync();

            var groupPermissions = await _context.UserPermissionGroups
                .Where(ug => ug.UserId == user.Id)
                .SelectMany(ug => ug.PermissionGroup.PermissionGroupPermissions)
                .Where(pgp => pgp.Permission != null)
                .Select(pgp => pgp.Permission!.Name)
                .ToListAsync();

            grantedPermissions = userPermissions
                .Concat(groupPermissions)
                .Where(p => !string.IsNullOrWhiteSpace(p))
                .ToHashSet(StringComparer.OrdinalIgnoreCase);
        }

        var appTiles = _systemAppDefinitions
            .Select(definition => new SystemAppTileViewModel
            {
                Name = definition.Name,
                Description = definition.Description,
                Category = definition.Category,
                Icon = definition.Icon,
                AccentColor = definition.AccentColor,
                Permission = definition.Permission,
                Url = definition.Url,
                HasAccess = string.IsNullOrWhiteSpace(definition.Permission) || grantedPermissions.Contains(definition.Permission)
            })
            .OrderBy(tile => tile.Category)
            .ThenBy(tile => tile.Name)
            .ToList();

        var viewModel = new SystemAppOverviewViewModel
        {
            Apps = appTiles
        };

        return View(viewModel);
    }

    [ResponseCache(Duration = 0, Location = ResponseCacheLocation.None, NoStore = true)]
    public IActionResult Error()
    {
        return View(new ErrorViewModel { RequestId = Activity.Current?.Id ?? HttpContext.TraceIdentifier });
    }

    private static IReadOnlyList<SystemAppDefinition> _systemAppDefinitions { get; } = new List<SystemAppDefinition>
    {
        new SystemAppDefinition(
            name: "لوحة التحكم",
            description: "نظرة شاملة على مؤشرات الأداء الرئيسية وموازين الحسابات",
            category: "الرئيسية",
            icon: "📊",
            accentColor: "#4e73df",
            permission: "dashboard.view",
            url: "/Dashboard/Index"),
        new SystemAppDefinition(
            name: "إدارة الحسابات",
            description: "استعرض شجرة الحسابات وتابع أرصدة الحسابات",
            category: "المحاسبة",
            icon: "📚",
            accentColor: "#20c997",
            permission: "accounts.view",
            url: "/Accounts/Index"),
        new SystemAppDefinition(
            name: "قيود اليومية",
            description: "إنشاء وتدقيق القيود اليومية للحركات المحاسبية",
            category: "المحاسبة",
            icon: "🧾",
            accentColor: "#f6c23e",
            permission: "journal.view",
            url: "/JournalEntries/Index"),
        new SystemAppDefinition(
            name: "السندات المقبوضة",
            description: "إدارة سندات القبض وتتبع التحصيلات",
            category: "الخزينة",
            icon: "🧾",
            accentColor: "#1cc88a",
            permission: "receiptvouchers.view",
            url: "/ReceiptVouchers/Index"),
        new SystemAppDefinition(
            name: "سندات الصرف",
            description: "إنشاء ومراجعة سندات الصرف النقدي",
            category: "الخزينة",
            icon: "💸",
            accentColor: "#36b9cc",
            permission: "paymentvouchers.view",
            url: "/PaymentVouchers/Index"),
        new SystemAppDefinition(
            name: "الحوالات",
            description: "متابعة الحوالات الداخلية والخارجية",
            category: "الخزينة",
            icon: "🔄",
            accentColor: "#858796",
            permission: "transfers.view",
            url: "/Transfers/Index"),
        new SystemAppDefinition(
            name: "المصاريف",
            description: "تسجيل المصاريف واعتمادها ومتابعة حدود الصرف",
            category: "المالية",
            icon: "🧮",
            accentColor: "#e74a3b",
            permission: "expenses.view",
            url: "/Expenses/Index"),
        new SystemAppDefinition(
            name: "التقارير",
            description: "عرض تقارير النظام التفصيلية والتحليلية",
            category: "التقارير",
            icon: "📈",
            accentColor: "#fd7e14",
            permission: "reports.view",
            url: "/Reports/Index"),
        new SystemAppDefinition(
            name: "الأصول",
            description: "إدارة الأصول الثابتة وتتبع حالة كل أصل",
            category: "الأصول",
            icon: "🏢",
            accentColor: "#6f42c1",
            permission: "assets.view",
            url: "/Assets/Index"),
        new SystemAppDefinition(
            name: "أنواع الأصول",
            description: "تصنيف الأصول وتحديد سياسات الإهلاك",
            category: "الأصول",
            icon: "🗂️",
            accentColor: "#6610f2",
            permission: "assettypes.view",
            url: "/AssetTypes/Index"),
        new SystemAppDefinition(
            name: "مصروفات الأصول",
            description: "تسجيل ومراجعة المصروفات المرتبطة بالأصول",
            category: "الأصول",
            icon: "🛠️",
            accentColor: "#d63384",
            permission: "assetexpenses.view",
            url: "/AssetExpenses/Index"),
        new SystemAppDefinition(
            name: "الموردون",
            description: "إدارة بيانات الموردين وسجلاتهم",
            category: "المشتريات",
            icon: "🚚",
            accentColor: "#198754",
            permission: "suppliers.view",
            url: "/Suppliers/Index"),
        new SystemAppDefinition(
            name: "الوكلاء",
            description: "متابعة بيانات الوكلاء وربطهم بالحسابات",
            category: "المبيعات",
            icon: "🤝",
            accentColor: "#0d6efd",
            permission: "agents.view",
            url: "/Agents/Index"),
        new SystemAppDefinition(
            name: "المستخدمون",
            description: "إدارة مستخدمي النظام وتعيين الصلاحيات",
            category: "الإعدادات",
            icon: "👥",
            accentColor: "#0dcaf0",
            permission: "users.view",
            url: "/Users/Index"),
        new SystemAppDefinition(
            name: "الفروع",
            description: "تعريف الفروع وضبط صلاحيات الوصول إليها",
            category: "الإعدادات",
            icon: "🏢",
            accentColor: "#9c27b0",
            permission: "branches.view",
            url: "/Branches/Index"),
        new SystemAppDefinition(
            name: "مراكز التكلفة",
            description: "تعريف مراكز التكلفة وربطها بالحسابات",
            category: "الإعدادات",
            icon: "🎯",
            accentColor: "#ff6f61",
            permission: "costcenters.view",
            url: "/CostCenters/Index"),
        new SystemAppDefinition(
            name: "العملات",
            description: "إدارة العملات وأسعار الصرف",
            category: "الإعدادات",
            icon: "💱",
            accentColor: "#00bcd4",
            permission: "currencies.view",
            url: "/Currencies/Index"),
        new SystemAppDefinition(
            name: "إعدادات النظام",
            description: "تهيئة الإعدادات العامة للنظام",
            category: "الإعدادات",
            icon: "⚙️",
            accentColor: "#17a2b8",
            permission: "systemsettings.view",
            url: "/SystemSettings/Index"),
    };

    private sealed class SystemAppDefinition
    {
        public SystemAppDefinition(string name, string description, string category, string icon, string accentColor, string permission, string url)
        {
            Name = name;
            Description = description;
            Category = category;
            Icon = icon;
            AccentColor = accentColor;
            Permission = permission;
            Url = url;
        }

        public string Name { get; }
        public string Description { get; }
        public string Category { get; }
        public string Icon { get; }
        public string AccentColor { get; }
        public string Permission { get; }
        public string Url { get; }
    }
}
