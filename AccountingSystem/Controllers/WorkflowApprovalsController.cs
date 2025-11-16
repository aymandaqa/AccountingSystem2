using System;
using System.Threading.Tasks;
using AccountingSystem.Services;
using AccountingSystem.ViewModels.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "workflowapprovals.view")]
    public class WorkflowApprovalsController : Controller
    {
        private readonly IWorkflowService _workflowService;
        private readonly IWorkflowApprovalViewModelFactory _approvalViewModelFactory;
        private readonly UserManager<User> _userManager;

        public WorkflowApprovalsController(
            IWorkflowService workflowService,
            IWorkflowApprovalViewModelFactory approvalViewModelFactory,
            UserManager<User> userManager)
        {
            _workflowService = workflowService;
            _approvalViewModelFactory = approvalViewModelFactory;
            _userManager = userManager;
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

    }
}
