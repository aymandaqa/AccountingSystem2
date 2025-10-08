using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.Workflows;
using AccountingSystem.Services;
using AccountingSystem.ViewModels.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "paymentvouchers.view")]
    public class WorkflowApprovalsController : Controller
    {
        private readonly IWorkflowService _workflowService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public WorkflowApprovalsController(IWorkflowService workflowService, ApplicationDbContext context, UserManager<User> userManager)
        {
            _workflowService = workflowService;
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var actions = await _workflowService.GetPendingActionsForUserAsync(user.Id);
            var viewModels = new List<WorkflowApprovalViewModel>();

            var paymentVoucherIds = actions
                .Where(a => a.WorkflowInstance.DocumentType == WorkflowDocumentType.PaymentVoucher)
                .Select(a => a.WorkflowInstance.DocumentId)
                .Distinct()
                .ToList();

            var vouchers = await _context.PaymentVouchers
                .Include(v => v.Supplier)
                .Include(v => v.Currency)
                .Include(v => v.CreatedBy)
                .Where(v => paymentVoucherIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id);

            foreach (var action in actions)
            {
                var model = new WorkflowApprovalViewModel
                {
                    ActionId = action.Id,
                    DocumentId = action.WorkflowInstance.DocumentId,
                    DocumentType = action.WorkflowInstance.DocumentType,
                    CreatedAt = action.WorkflowInstance.CreatedAt,
                    Title = GetTitle(action.WorkflowInstance),
                    Description = GetDescription(action.WorkflowInstance)
                };

                if (action.WorkflowInstance.DocumentType == WorkflowDocumentType.PaymentVoucher && vouchers.TryGetValue(action.WorkflowInstance.DocumentId, out var voucher))
                {
                    model.PaymentVoucher = voucher;
                }

                viewModels.Add(model);
            }

            return View(viewModels.OrderBy(v => v.CreatedAt).ToList());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Approve(int actionId, string? notes)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            await _workflowService.ProcessActionAsync(actionId, user.Id, approve: true, notes: notes);
            TempData["Success"] = "تمت الموافقة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
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

        private string GetTitle(WorkflowInstance instance)
        {
            return instance.DocumentType switch
            {
                WorkflowDocumentType.PaymentVoucher => $"سند دفع رقم {instance.DocumentId}",
                _ => $"مستند رقم {instance.DocumentId}"
            };
        }

        private string GetDescription(WorkflowInstance instance)
        {
            return instance.DocumentType switch
            {
                WorkflowDocumentType.PaymentVoucher => "يرجى مراجعة بيانات سند الدفع واعتمادها",
                _ => "يرجى مراجعة المستند واعتماد القرار"
            };
        }
    }
}
