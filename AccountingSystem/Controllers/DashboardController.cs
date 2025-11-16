using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models.Reports;
using AccountingSystem.ViewModels.Dashboard;
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

        public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger, IWorkflowApprovalViewModelFactory approvalViewModelFactory)
        {
            _context = context;
            _logger = logger;
            _approvalViewModelFactory = approvalViewModelFactory;
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

                var viewModel = new CashPerformanceDashboardViewModel
                {
                    Records = records,
                    MyPendingRequests = myPendingRequests,
                    PendingApprovals = pendingApprovals,
                    TotalCustomerDuesOnRoad = records.Sum(r => r.CustomerDuesOnRoad),
                    TotalCashWithDriverOnRoad = records.Sum(r => r.CashWithDriverOnRoad),
                    TotalCustomerDues = records.Sum(r => r.CustomerDues),
                    TotalCashOnBranchBox = records.Sum(r => r.CashOnBranchBox)
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
