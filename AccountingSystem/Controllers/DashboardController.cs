using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models.Reports;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AccountingSystem.Models.Workflows;
using AccountingSystem.ViewModels.Workflows;
using AccountingSystem.Services;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Identity;
using AccountingSystem.ViewModels.Dashboard;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "dashboard.view")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardController> _logger;
        private readonly IWorkflowApprovalViewModelFactory _approvalViewModelFactory;
        private readonly ICurrencyService _currencyService;
        private readonly UserManager<User> _userManager;

        public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger, IWorkflowApprovalViewModelFactory approvalViewModelFactory, ICurrencyService currencyService, UserManager<User> userManager)
        {
            _context = context;
            _logger = logger;
            _approvalViewModelFactory = approvalViewModelFactory;
            _currencyService = currencyService;
            _userManager = userManager;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            try
            {
                var records = await LoadRecordsForUserAsync();
                var transfersBalance = await LoadTransfersBalanceAsync();
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                var myPendingRequests = string.IsNullOrWhiteSpace(userId)
                    ? Array.Empty<PendingWorkflowRequestViewModel>()
                    : await LoadPendingInitiatedRequestsAsync(userId);
                var pendingApprovals = string.IsNullOrWhiteSpace(userId)
                    ? Array.Empty<WorkflowApprovalViewModel>()
                    : await _approvalViewModelFactory.BuildPendingApprovalsAsync(userId);
                var pendingTransfers = string.IsNullOrWhiteSpace(userId)
                    ? Array.Empty<PaymentTransfer>()
                    : await LoadPendingTransfersForUserAsync(userId);
                var pendingIncomingTransfers = string.IsNullOrWhiteSpace(userId)
                    ? Array.Empty<PaymentTransfer>()
                    : await LoadIncomingPendingTransfersForUserAsync(userId);
                var hasPaymentAccount = string.IsNullOrWhiteSpace(userId)
                    ? false
                    : await UserHasPaymentAccountAsync(userId);
                var dashboardAccounts = await LoadDashboardAccountTreeAsync();

                var viewModel = new CashPerformanceDashboardViewModel
                {
                    Records = records,
                    MyPendingRequests = myPendingRequests,
                    PendingApprovals = pendingApprovals,
                    PendingTransfers = pendingTransfers,
                    PendingIncomingTransfers = pendingIncomingTransfers,
                    CurrentUserId = userId,
                    TotalCustomerDuesOnRoad = records.Sum(r => r.CustomerDuesOnRoad),
                    TotalCashWithDriverOnRoad = records.Sum(r => r.CashWithDriverOnRoad),
                    TotalCustomerDues = records.Sum(r => r.CustomerDues),
                    TransfersBalance = transfersBalance,
                    TotalCashOnBranchBox = records.Sum(r => r.CashOnBranchBox) + transfersBalance,
                    DashboardAccountTree = dashboardAccounts.Nodes,
                    DashboardBaseCurrencyCode = dashboardAccounts.BaseCurrencyCode,
                    DashboardParentAccountName = dashboardAccounts.ParentAccountName,
                    HasPaymentAccount = hasPaymentAccount
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load cash performance data for the dashboard.");
                return View(new CashPerformanceDashboardViewModel());
            }
        }

        [Authorize]
        public async Task<IActionResult> ProfitabilityReport(DateTime? startDate, DateTime? endDate)
        {
            var (periodStart, periodEnd) = NormalizePeriod(startDate, endDate);
            var previousPeriodEnd = periodStart.AddDays(-1);
            var previousPeriodStart = previousPeriodEnd.AddDays(-(periodEnd - periodStart).Days);

            var incomeQuery = _context.ReceiptVouchers.AsNoTracking()
                .Where(v => v.Date >= periodStart && v.Date <= periodEnd);
            var expenseQuery = _context.Expenses.AsNoTracking()
                .Where(e => e.CreatedAt >= periodStart && e.CreatedAt <= periodEnd);
            var paymentVoucherQuery = _context.PaymentVouchers.AsNoTracking()
                .Where(p => p.Date >= periodStart && p.Date <= periodEnd);
            var salaryQuery = _context.SalaryPayments.AsNoTracking()
                .Where(s => s.Date >= periodStart && s.Date <= periodEnd);

            var previousIncomeQuery = _context.ReceiptVouchers.AsNoTracking()
                .Where(v => v.Date >= previousPeriodStart && v.Date <= previousPeriodEnd);
            var previousExpenseQuery = _context.Expenses.AsNoTracking()
                .Where(e => e.CreatedAt >= previousPeriodStart && e.CreatedAt <= previousPeriodEnd);
            var previousPaymentVoucherQuery = _context.PaymentVouchers.AsNoTracking()
                .Where(p => p.Date >= previousPeriodStart && p.Date <= previousPeriodEnd);
            var previousSalaryQuery = _context.SalaryPayments.AsNoTracking()
                .Where(s => s.Date >= previousPeriodStart && s.Date <= previousPeriodEnd);

            var totalIncome = await incomeQuery.SumAsync(v => v.Amount);
            var totalExpenses = await expenseQuery.SumAsync(e => e.Amount)
                + await paymentVoucherQuery.SumAsync(p => p.Amount)
                + await salaryQuery.SumAsync(s => s.Amount);
            var netProfit = totalIncome - totalExpenses;

            var previousIncome = await previousIncomeQuery.SumAsync(v => v.Amount);
            var previousExpenses = await previousExpenseQuery.SumAsync(e => e.Amount)
                + await previousPaymentVoucherQuery.SumAsync(p => p.Amount)
                + await previousSalaryQuery.SumAsync(s => s.Amount);
            var previousNetProfit = previousIncome - previousExpenses;

            var weeklyComparisons = await BuildWeeklyComparisons(periodStart, periodEnd, incomeQuery, expenseQuery, paymentVoucherQuery, salaryQuery);
            var shipmentTarget = await BuildTargetShipmentSummary(incomeQuery, totalIncome, totalExpenses);
            var topDrivers = await LoadTopDriversAsync(periodStart, periodEnd);
            var topUsers = await LoadTopUsersAsync(periodStart, periodEnd);
            var branchTargets = await LoadBranchTargetsAsync(periodStart, periodEnd, totalIncome);
            var annualProjection = CalculateAnnualProjection(netProfit, totalIncome, totalExpenses, periodStart, periodEnd);

            var viewModel = new ProfitabilityReportViewModel
            {
                StartDate = periodStart,
                EndDate = periodEnd,
                TotalExpenses = totalExpenses,
                NetProfit = netProfit,
                NetProfitChangePercent = CalculateChangePercent(previousNetProfit, netProfit),
                ExpensesChangePercent = CalculateChangePercent(previousExpenses, totalExpenses),
                WeeklyComparisons = weeklyComparisons,
                ShipmentTarget = shipmentTarget,
                TopDrivers = topDrivers,
                TopUsers = topUsers,
                BranchTargets = branchTargets,
                AnnualProjection = annualProjection
            };

            return View(viewModel);
        }

        private async Task<IReadOnlyList<CashPerformanceRecord>> LoadRecordsForUserAsync()
        {


            var records = await _context.CashPerformanceRecords
                .AsNoTracking()
                .ToListAsync();

            return records;
        }

        private async Task<decimal> LoadTransfersBalanceAsync()
        {
            return await _context.PaymentTransfers
                .AsNoTracking()
                .Where(t => t.Status == TransferStatus.Pending)
                .SumAsync(t => t.Amount);
        }

        private async Task<IReadOnlyList<PaymentTransfer>> LoadPendingTransfersForUserAsync(string userId)
        {
            var user = await _userManager.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return Array.Empty<PaymentTransfer>();
            }

            var query = _context.PaymentTransfers
                .AsNoTracking()
                .Include(t => t.Sender)
                .Include(t => t.Receiver)
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .Where(t => t.Status == TransferStatus.Pending);

            if (user.PaymentBranchId.HasValue)
            {
                var branchId = user.PaymentBranchId.Value;
                query = query.Where(t =>
                    (t.FromBranchId.HasValue && t.FromBranchId.Value == branchId) ||
                    (t.ToBranchId.HasValue && t.ToBranchId.Value == branchId));
            }
            else
            {
                query = query.Where(t => t.SenderId == userId || t.ReceiverId == userId);
            }

            return await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        private async Task<IReadOnlyList<PaymentTransfer>> LoadIncomingPendingTransfersForUserAsync(string userId)
        {
            var user = await _userManager.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return Array.Empty<PaymentTransfer>();
            }

            var query = _context.PaymentTransfers
                .AsNoTracking()
                .Include(t => t.Sender)
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .Where(t => t.Status == TransferStatus.Pending);

            if (user.PaymentBranchId.HasValue)
            {
                var branchId = user.PaymentBranchId.Value;
                query = query.Where(t => t.ToBranchId.HasValue && t.ToBranchId.Value == branchId);
            }
            else
            {
                query = query.Where(t => t.ReceiverId == userId);
            }

            return await query
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
        }

        private async Task<bool> UserHasPaymentAccountAsync(string userId)
        {
            var user = await _userManager.Users
                .AsNoTracking()
                .FirstOrDefaultAsync(u => u.Id == userId);

            if (user == null)
            {
                return false;
            }

            if (user.PaymentAccountId.HasValue)
            {
                return true;
            }

            return await _context.UserPaymentAccounts
                .AsNoTracking()
                .AnyAsync(upa => upa.UserId == userId);
        }

        private static (DateTime Start, DateTime End) NormalizePeriod(DateTime? startDate, DateTime? endDate)
        {
            var defaultStart = new DateTime(DateTime.Today.AddMonths(-1).Year, DateTime.Today.AddMonths(-1).Month, 1);
            var defaultEnd = defaultStart.AddMonths(1).AddDays(-1);

            var start = startDate?.Date ?? defaultStart;
            var end = endDate?.Date ?? defaultEnd;

            if (end < start)
            {
                (start, end) = (end, start);
            }

            return (start, end);
        }

        private static decimal CalculateChangePercent(decimal previousValue, decimal currentValue)
        {
            if (previousValue == 0)
            {
                return currentValue == 0 ? 0 : 100;
            }

            return Math.Round(((currentValue - previousValue) / Math.Abs(previousValue)) * 100, 2);
        }

        private static DateTime GetWeekStart(DateTime date)
        {
            var dayOfWeek = ((int)date.DayOfWeek + 6) % 7; // Monday = 0
            return date.Date.AddDays(-dayOfWeek);
        }

        private async Task<IReadOnlyList<WeeklyProfitComparison>> BuildWeeklyComparisons(
            DateTime periodStart,
            DateTime periodEnd,
            IQueryable<ReceiptVoucher> incomeQuery,
            IQueryable<Expense> expenseQuery,
            IQueryable<PaymentVoucher> paymentVoucherQuery,
            IQueryable<SalaryPayment> salaryQuery)
        {
            var incomeEntries = await incomeQuery
                .Select(v => new { v.Date, Amount = v.Amount })
                .ToListAsync();

            var expenseEntries = await expenseQuery
                .Select(e => new { Date = e.CreatedAt, Amount = e.Amount })
                .ToListAsync();

            var paymentEntries = await paymentVoucherQuery
                .Select(p => new { p.Date, Amount = p.Amount })
                .ToListAsync();

            var salaryEntries = await salaryQuery
                .Select(s => new { s.Date, Amount = s.Amount })
                .ToListAsync();

            var expenses = expenseEntries
                .Concat(paymentEntries)
                .Concat(salaryEntries)
                .GroupBy(e => GetWeekStart(e.Date))
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

            var profits = incomeEntries
                .GroupBy(e => GetWeekStart(e.Date))
                .ToDictionary(g => g.Key, g => g.Sum(e => e.Amount));

            var weeks = new List<WeeklyProfitComparison>();
            var cursor = GetWeekStart(periodStart);
            while (cursor <= periodEnd)
            {
                var nextWeek = cursor.AddDays(7);
                var label = $"{cursor:dd MMM} - {nextWeek.AddDays(-1):dd MMM}";
                var weekExpenses = expenses.TryGetValue(cursor, out var value) ? value : 0m;
                var weekIncome = profits.TryGetValue(cursor, out var income) ? income : 0m;
                var weekNet = weekIncome - weekExpenses;

                var previousWeek = cursor.AddDays(-7);
                var previousExpenses = expenses.TryGetValue(previousWeek, out var prevExp) ? prevExp : 0m;
                var previousIncome = profits.TryGetValue(previousWeek, out var prevInc) ? prevInc : 0m;
                var previousNet = previousIncome - previousExpenses;

                weeks.Add(new WeeklyProfitComparison
                {
                    Label = label,
                    Expenses = weekExpenses,
                    NetProfit = weekNet,
                    ProfitChangePercent = CalculateChangePercent(previousNet, weekNet)
                });

                cursor = nextWeek;
            }

            return weeks;
        }

        private static async Task<TargetShipmentSummary> BuildTargetShipmentSummary(
            IQueryable<ReceiptVoucher> incomeQuery,
            decimal totalIncome,
            decimal totalExpenses)
        {
            var shipmentCount = await incomeQuery.CountAsync();
            var averageRevenue = shipmentCount == 0 ? 0 : totalIncome / shipmentCount;
            var targetShipments = averageRevenue == 0 ? 0 : (int)Math.Ceiling(totalExpenses / averageRevenue);

            return new TargetShipmentSummary
            {
                TargetShipments = targetShipments,
                BreakEvenRevenue = totalExpenses,
                AverageRevenuePerShipment = averageRevenue
            };
        }

        private async Task<IReadOnlyList<TopContributor>> LoadTopDriversAsync(DateTime start, DateTime end)
        {
            var data = await _context.PaymentVouchers.AsNoTracking()
                .Include(p => p.Agent)
                .Where(p => p.AgentId != null && p.Date >= start && p.Date <= end)
                .GroupBy(p => p.Agent!.Name)
                .Select(g => new TopContributor
                {
                    Name = g.Key,
                    Value = g.Sum(x => x.Amount),
                    Descriptor = "صافي مدفوعات"
                })
                .OrderByDescending(g => g.Value)
                .Take(10)
                .ToListAsync();

            return data;
        }

        private async Task<IReadOnlyList<TopContributor>> LoadTopUsersAsync(DateTime start, DateTime end)
        {
            var sessions = await _context.UserSessions.AsNoTracking()
                .Where(s => s.CreatedAt >= start && s.CreatedAt <= end)
                .GroupBy(s => s.UserId)
                .Select(g => new { g.Key, Count = g.Count() })
                .OrderByDescending(g => g.Count)
                .Take(10)
                .ToListAsync();

            var users = await _userManager.Users
                .Where(u => sessions.Select(s => s.Key).Contains(u.Id))
                .ToDictionaryAsync(u => u.Id, u => u.UserName ?? u.Email ?? u.Id);

            return sessions
                .Select(s => new TopContributor
                {
                    Name = users.TryGetValue(s.Key, out var name) ? name : s.Key,
                    Value = s.Count,
                    Descriptor = "جلسات"
                })
                .ToList();
        }

        private async Task<IReadOnlyList<BranchTargetSummary>> LoadBranchTargetsAsync(DateTime start, DateTime end, decimal totalIncome)
        {
            var branchExpenses = await _context.Expenses.AsNoTracking()
                .Include(e => e.Branch)
                .Where(e => e.CreatedAt >= start && e.CreatedAt <= end)
                .GroupBy(e => e.Branch.NameAr)
                .Select(g => new BranchTargetSummary
                {
                    BranchName = g.Key,
                    RequiredTarget = g.Sum(x => x.Amount),
                    CurrentCoverage = totalIncome == 0 ? 0 : Math.Round((g.Sum(x => x.Amount) / totalIncome) * 100, 2)
                })
                .OrderByDescending(g => g.RequiredTarget)
                .ToListAsync();

            return branchExpenses;
        }

        private AnnualProfitProjection CalculateAnnualProjection(decimal netProfit, decimal totalIncome, decimal totalExpenses, DateTime start, DateTime end)
        {
            var days = Math.Max(1, (end - start).Days + 1);
            var remainingDays = (new DateTime(DateTime.Today.Year, 12, 31) - DateTime.Today).Days;
            var dailyProfit = netProfit / days;

            return new AnnualProfitProjection
            {
                ProjectedNetProfit = Math.Round(netProfit + (dailyProfit * remainingDays), 2),
                AnnualizedRevenue = Math.Round(totalIncome / days * 365, 2),
                AnnualizedExpenses = Math.Round(totalExpenses / days * 365, 2)
            };
        }

        private async Task<(List<AccountTreeNodeViewModel> Nodes, string BaseCurrencyCode, string ParentAccountName)> LoadDashboardAccountTreeAsync()
        {
            var systemSetting = await _context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "DashboardParentAccountId");

            if (systemSetting == null || string.IsNullOrWhiteSpace(systemSetting.Value) || !int.TryParse(systemSetting.Value, out var parentAccountId))
            {
                return (new List<AccountTreeNodeViewModel>(), string.Empty, string.Empty);
            }

            var accounts = await _context.Accounts
                .AsNoTracking()
                .Include(a => a.Currency)
                .ToListAsync();

            var baseCurrency = await _context.Currencies.AsNoTracking().FirstOrDefaultAsync(c => c.IsBase);
            if (baseCurrency == null)
            {
                return (new List<AccountTreeNodeViewModel>(), string.Empty, string.Empty);
            }

            if (!accounts.Any(a => a.Id == parentAccountId))
            {
                return (new List<AccountTreeNodeViewModel>(), baseCurrency.Code, string.Empty);
            }

            var childrenLookup = accounts
                .Where(a => a.ParentId.HasValue)
                .GroupBy(a => a.ParentId!.Value)
                .ToDictionary(g => g.Key, g => g.OrderBy(c => c.Code).ToList());

            AccountTreeNodeViewModel BuildNode(Account account, int level)
            {
                var balanceSelected = _currencyService.Convert(account.CurrentBalance, account.Currency, baseCurrency);
                var node = new AccountTreeNodeViewModel
                {
                    Id = account.Id,
                    Code = account.Code,
                    NameAr = account.NameAr,
                    ParentAccountName = account.Parent != null ? account.Parent.NameAr : string.Empty,
                    AccountType = account.AccountType,
                    Nature = account.Nature,
                    CurrencyCode = account.Currency.Code,
                    OpeningBalance = account.OpeningBalance,
                    CurrentBalance = account.CurrentBalance,
                    CurrentBalanceSelected = balanceSelected,
                    CurrentBalanceBase = balanceSelected,
                    Balance = account.CurrentBalance,
                    BalanceSelected = balanceSelected,
                    BalanceBase = balanceSelected,
                    CanPostTransactions = account.CanPostTransactions,
                    ParentId = account.ParentId,
                    Level = level,
                    HasChildren = false
                };

                if (childrenLookup.TryGetValue(account.Id, out var children) && children.Any())
                {
                    foreach (var child in children)
                    {
                        node.Children.Add(BuildNode(child, level + 1));
                    }

                    node.HasChildren = node.Children.Any();
                    var childrenBalance = node.Children.Sum(c => c.Balance);
                    var childrenBalanceSelected = node.Children.Sum(c => c.BalanceSelected);
                    var childrenBalanceBase = node.Children.Sum(c => c.BalanceBase);

                    node.Balance = node.CurrentBalance + childrenBalance;
                    node.BalanceSelected = node.CurrentBalanceSelected + childrenBalanceSelected;
                    node.BalanceBase = node.CurrentBalanceBase + childrenBalanceBase;
                    node.CurrentBalanceSelected = node.BalanceSelected;
                    node.CurrentBalanceBase = node.BalanceBase;
                }

                return node;
            }

            var parentAccountName = accounts.First(a => a.Id == parentAccountId).NameAr;
            var nodes = childrenLookup.TryGetValue(parentAccountId, out var rootChildren)
                ? rootChildren.Select(child => BuildNode(child, 1)).ToList()
                : new List<AccountTreeNodeViewModel>();

            return (nodes, baseCurrency.Code, parentAccountName);
        }

        private async Task<IReadOnlyList<PendingWorkflowRequestViewModel>> LoadPendingInitiatedRequestsAsync(string userId)
        {
            var instances = await _context.WorkflowInstances
                .Include(i => i.Actions).ThenInclude(a => a.WorkflowStep).ThenInclude(s => s.ApproverUser)
                .Include(i => i.DocumentCurrency)
                .Where(i => i.InitiatorId == userId && i.Status == WorkflowInstanceStatus.InProgress)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            return instances
                .Select(MapPendingRequest)
                .ToList();
        }

        private PendingWorkflowRequestViewModel MapPendingRequest(WorkflowInstance instance)
        {
            var pendingAction = instance.Actions
                .Where(a => a.Status == WorkflowActionStatus.Pending)
                .OrderBy(a => a.WorkflowStep.Order)
                .FirstOrDefault();

            var pendingWith = pendingAction?.WorkflowStep.ApproverUser != null
                ? GetUserDisplayName(pendingAction.WorkflowStep.ApproverUser, pendingAction.WorkflowStep.ApproverUserId)
                : pendingAction == null
                    ? "-"
                    : !string.IsNullOrWhiteSpace(pendingAction.WorkflowStep.RequiredPermission)
                        ? $"صلاحية: {pendingAction.WorkflowStep.RequiredPermission}"
                        : "في انتظار أول مستخدم مخول";

            return new PendingWorkflowRequestViewModel
            {
                WorkflowInstanceId = instance.Id,
                DocumentId = instance.DocumentId,
                DocumentType = instance.DocumentType,
                Title = GetTitle(instance),
                Description = GetDescription(instance),
                Amount = instance.DocumentAmount,
                CurrencyCode = instance.DocumentCurrency?.Code,
                CreatedAt = instance.CreatedAt,
                PendingWith = pendingWith
            };
        }

        private static string GetUserDisplayName(User? user, string? fallbackId, string? currentValue = null)
        {
            if (user != null)
            {
                if (!string.IsNullOrWhiteSpace(user.FullName))
                {
                    return user.FullName!;
                }

                if (!string.IsNullOrWhiteSpace(user.UserName))
                {
                    return user.UserName!;
                }
            }

            if (!string.IsNullOrWhiteSpace(currentValue))
            {
                return currentValue!;
            }

            return string.IsNullOrWhiteSpace(fallbackId) ? "غير معروف" : fallbackId!;
        }

        private static string GetTitle(WorkflowInstance instance)
        {
            return instance.DocumentType switch
            {
                WorkflowDocumentType.PaymentVoucher => $"سند صرف رقم {instance.DocumentId}",
                WorkflowDocumentType.ReceiptVoucher => $"سند قبض رقم {instance.DocumentId}",
                WorkflowDocumentType.DisbursementVoucher => $"سند دفع  رقم {instance.DocumentId}",
                WorkflowDocumentType.DynamicScreenEntry => $"حركة شاشة ديناميكية رقم {instance.DocumentId}",
                WorkflowDocumentType.AssetExpense => $"مصروف أصل رقم {instance.DocumentId}",
                _ => $"مستند رقم {instance.DocumentId}"
            };
        }

        private static string GetDescription(WorkflowInstance instance)
        {
            return instance.DocumentType switch
            {
                WorkflowDocumentType.PaymentVoucher => "يرجى مراجعة بيانات سند الصرف واعتمادها",
                WorkflowDocumentType.ReceiptVoucher => "يرجى مراجعة بيانات سند القبض واعتمادها",
                WorkflowDocumentType.DisbursementVoucher => "يرجى مراجعة بيانات سند دفع  واعتمادها",
                WorkflowDocumentType.DynamicScreenEntry => "يرجى مراجعة بيانات الحركة الديناميكية واتخاذ القرار",
                WorkflowDocumentType.AssetExpense => "يرجى مراجعة بيانات مصروف الأصل واعتمادها",
                _ => "يرجى مراجعة بيانات المستند واعتمادها"
            };
        }
    }
}
