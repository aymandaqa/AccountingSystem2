using System;
using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.ViewModels.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Models.Workflows;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "workflowapprovals.view")]
    public class WorkflowApprovalsController : Controller
    {
        private readonly IWorkflowService _workflowService;
        private readonly IWorkflowApprovalViewModelFactory _approvalViewModelFactory;
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public WorkflowApprovalsController(
            IWorkflowService workflowService,
            IWorkflowApprovalViewModelFactory approvalViewModelFactory,
            UserManager<User> userManager,
            ApplicationDbContext context)
        {
            _workflowService = workflowService;
            _approvalViewModelFactory = approvalViewModelFactory;
            _userManager = userManager;
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var viewModels = await _approvalViewModelFactory.BuildPendingApprovalsAsync(user.Id);

            return View(viewModels.OrderBy(v => v.CreatedAt).ToList());
        }

        [HttpPost]
        [Authorize(Policy = "workflowapprovals.process")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int actionId, string? notes)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            try
            {
                await _workflowService.ProcessActionAsync(actionId, user.Id, approve: true, notes: notes);
                TempData["Success"] = "تمت الموافقة بنجاح";
            }
            catch (InvalidOperationException ex) when (ex.Message == AssetExpenseMessages.InsufficientPaymentBalanceMessage)
            {
                TempData["Error"] = ex.Message;
            }

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [Authorize(Policy = "workflowapprovals.process")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reject(int actionId, string? notes)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            await _workflowService.ProcessActionAsync(actionId, user.Id, approve: false, notes: notes);
            TempData["Error"] = "تم رفض الطلب";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> Report(DateTime? fromDate = null, DateTime? toDate = null, WorkflowActionStatus? status = null)
        {
            var query = _context.WorkflowActions
                .Include(a => a.WorkflowInstance).ThenInclude(i => i.Initiator)
                .Include(a => a.User)
                .AsQueryable();

            var start = fromDate?.Date ?? DateTime.UtcNow.Date.AddDays(-30);
            var end = toDate?.Date.AddDays(1).AddTicks(-1) ?? DateTime.UtcNow.Date.AddDays(1).AddTicks(-1);

            query = query.Where(a => (a.ActionedAt ?? a.WorkflowInstance.CreatedAt) >= start && (a.ActionedAt ?? a.WorkflowInstance.CreatedAt) <= end);

            if (status.HasValue)
            {
                query = query.Where(a => a.Status == status.Value);
            }

            var items = await query
                .OrderByDescending(a => a.ActionedAt ?? a.WorkflowInstance.CreatedAt)
                .Select(a => new WorkflowApprovalReportItem
                {
                    ActionId = a.Id,
                    DocumentId = a.WorkflowInstance.DocumentId,
                    DocumentType = a.WorkflowInstance.DocumentType,
                    Title = GetTitle(a.WorkflowInstance),
                    RequesterName = string.IsNullOrWhiteSpace(a.WorkflowInstance.Initiator.FullName)
                        ? a.WorkflowInstance.InitiatorId ?? "غير معروف"
                        : a.WorkflowInstance.Initiator.FullName,
                    ApproverName = a.User == null
                        ? null
                        : string.IsNullOrWhiteSpace(a.User.FullName)
                            ? a.User.UserName
                            : a.User.FullName,
                    CreatedAt = a.WorkflowInstance.CreatedAt,
                    ActionedAt = a.ActionedAt,
                    Status = a.Status,
                    Notes = a.Notes
                })
                .ToListAsync();

            var model = new WorkflowApprovalReportViewModel
            {
                Items = items,
                FromDate = start,
                ToDate = end,
                Status = status
            };

            return View(model);
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

    }
}
