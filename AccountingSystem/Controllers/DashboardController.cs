using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models.Reports;
using AccountingSystem.ViewModels.Dashboard;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using AccountingSystem.Models.Workflows;
using AccountingSystem.ViewModels.Workflows;
using AccountingSystem.Services;
using AccountingSystem.Models;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "dashboard.view")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardController> _logger;
        private readonly IWorkflowApprovalViewModelFactory _approvalViewModelFactory;
        private readonly ICurrencyService _currencyService;

        public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger, IWorkflowApprovalViewModelFactory approvalViewModelFactory, ICurrencyService currencyService)
        {
            _context = context;
            _logger = logger;
            _approvalViewModelFactory = approvalViewModelFactory;
            _currencyService = currencyService;
        }

        [Authorize]
        public async Task<IActionResult> Index()
        {
            try
            {
                var records = await LoadRecordsForUserAsync();
                var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
                var myPendingRequests = string.IsNullOrWhiteSpace(userId)
                    ? Array.Empty<PendingWorkflowRequestViewModel>()
                    : await LoadPendingInitiatedRequestsAsync(userId);
                var pendingApprovals = string.IsNullOrWhiteSpace(userId)
                    ? Array.Empty<WorkflowApprovalViewModel>()
                    : await _approvalViewModelFactory.BuildPendingApprovalsAsync(userId);
                var dashboardAccounts = await LoadDashboardAccountTreeAsync();

                var viewModel = new CashPerformanceDashboardViewModel
                {
                    Records = records,
                    MyPendingRequests = myPendingRequests,
                    PendingApprovals = pendingApprovals,
                    TotalCustomerDuesOnRoad = records.Sum(r => r.CustomerDuesOnRoad),
                    TotalCashWithDriverOnRoad = records.Sum(r => r.CashWithDriverOnRoad),
                    TotalCustomerDues = records.Sum(r => r.CustomerDues),
                    TotalCashOnBranchBox = records.Sum(r => r.CashOnBranchBox),
                    DashboardAccountTree = dashboardAccounts.Nodes,
                    DashboardBaseCurrencyCode = dashboardAccounts.BaseCurrencyCode,
                    DashboardParentAccountName = dashboardAccounts.ParentAccountName
                };

                return View(viewModel);
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load cash performance data for the dashboard.");
                return View(new CashPerformanceDashboardViewModel());
            }
        }

        private async Task<IReadOnlyList<CashPerformanceRecord>> LoadRecordsForUserAsync()
        {


            var records = await _context.CashPerformanceRecords
                .AsNoTracking()
                .ToListAsync();

            return records;
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
