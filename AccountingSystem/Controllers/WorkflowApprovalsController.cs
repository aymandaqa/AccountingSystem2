using System;
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
    [Authorize(Policy = "workflowapprovals.view")]
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

            var dynamicEntryIds = actions
                .Where(a => a.WorkflowInstance.DocumentType == WorkflowDocumentType.DynamicScreenEntry)
                .Select(a => a.WorkflowInstance.DocumentId)
                .Distinct()
                .ToList();

            var receiptVoucherIds = actions
                .Where(a => a.WorkflowInstance.DocumentType == WorkflowDocumentType.ReceiptVoucher)
                .Select(a => a.WorkflowInstance.DocumentId)
                .Distinct()
                .ToList();

            var disbursementVoucherIds = actions
                .Where(a => a.WorkflowInstance.DocumentType == WorkflowDocumentType.DisbursementVoucher)
                .Select(a => a.WorkflowInstance.DocumentId)
                .Distinct()
                .ToList();

            var assetExpenseIds = actions
                .Where(a => a.WorkflowInstance.DocumentType == WorkflowDocumentType.AssetExpense)
                .Select(a => a.WorkflowInstance.DocumentId)
                .Distinct()
                .ToList();

            var vouchers = await _context.PaymentVouchers
                .Include(v => v.Supplier)
                .Include(v => v.Currency)
                .Include(v => v.CreatedBy)
                .Where(v => paymentVoucherIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id);

            var dynamicEntries = await _context.DynamicScreenEntries
                .Include(e => e.Screen)
                .Include(e => e.Supplier)
                .Include(e => e.CreatedBy)
                .Where(e => dynamicEntryIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id);

            var receiptVouchers = await _context.ReceiptVouchers
                .Include(v => v.Supplier)
                .Include(v => v.Currency)
                .Include(v => v.Account)
                .Include(v => v.PaymentAccount)
                .Include(v => v.CreatedBy)
                .Where(v => receiptVoucherIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id);

            var disbursementVouchers = await _context.DisbursementVouchers
                .Include(v => v.Supplier)
                .Include(v => v.Currency)
                .Include(v => v.CreatedBy)
                .Where(v => disbursementVoucherIds.Contains(v.Id))
                .ToDictionaryAsync(v => v.Id);

            var assetExpenses = await _context.AssetExpenses
                .Include(e => e.Asset).ThenInclude(a => a.Branch)
                .Include(e => e.ExpenseAccount)
                .Include(e => e.Supplier)
                .Include(e => e.Currency)
                .Include(e => e.CreatedBy)
                .Where(e => assetExpenseIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id);

            foreach (var action in actions)
            {
                var model = new WorkflowApprovalViewModel
                {
                    ActionId = action.Id,
                    DocumentId = action.WorkflowInstance.DocumentId,
                    DocumentType = action.WorkflowInstance.DocumentType,
                    CreatedAt = action.WorkflowInstance.CreatedAt,
                    Title = GetTitle(action.WorkflowInstance),
                    Description = GetDescription(action.WorkflowInstance),
                    Amount = action.WorkflowInstance.DocumentAmount,
                    AmountInBase = action.WorkflowInstance.DocumentAmountInBase,
                    CurrencyCode = action.WorkflowInstance.DocumentCurrency?.Code
                };

                if (action.WorkflowInstance.DocumentType == WorkflowDocumentType.PaymentVoucher && vouchers.TryGetValue(action.WorkflowInstance.DocumentId, out var voucher))
                {
                    model.PaymentVoucher = voucher;
                }
                else if (action.WorkflowInstance.DocumentType == WorkflowDocumentType.DynamicScreenEntry && dynamicEntries.TryGetValue(action.WorkflowInstance.DocumentId, out var entry))
                {
                    model.DynamicEntry = entry;
                }
                else if (action.WorkflowInstance.DocumentType == WorkflowDocumentType.ReceiptVoucher && receiptVouchers.TryGetValue(action.WorkflowInstance.DocumentId, out var receipt))
                {
                    model.ReceiptVoucher = receipt;
                    if (string.IsNullOrEmpty(model.CurrencyCode))
                    {
                        model.CurrencyCode = receipt.Currency?.Code;
                    }
                }
                else if (action.WorkflowInstance.DocumentType == WorkflowDocumentType.DisbursementVoucher && disbursementVouchers.TryGetValue(action.WorkflowInstance.DocumentId, out var disbursement))
                {
                    model.DisbursementVoucher = disbursement;
                    if (string.IsNullOrEmpty(model.CurrencyCode))
                    {
                        model.CurrencyCode = disbursement.Currency?.Code;
                    }
                }
                else if (action.WorkflowInstance.DocumentType == WorkflowDocumentType.AssetExpense && assetExpenses.TryGetValue(action.WorkflowInstance.DocumentId, out var assetExpense))
                {
                    model.AssetExpense = assetExpense;
                    if (string.IsNullOrEmpty(model.CurrencyCode))
                    {
                        model.CurrencyCode = assetExpense.Currency?.Code;
                    }
                }

                viewModels.Add(model);
            }

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

        private string GetTitle(WorkflowInstance instance)
        {
            return instance.DocumentType switch
            {
                WorkflowDocumentType.PaymentVoucher => $"سند دفع رقم {instance.DocumentId}",
                WorkflowDocumentType.ReceiptVoucher => $"سند قبض رقم {instance.DocumentId}",
                WorkflowDocumentType.DisbursementVoucher => $"سند صرف رقم {instance.DocumentId}",
                WorkflowDocumentType.DynamicScreenEntry => $"حركة شاشة ديناميكية رقم {instance.DocumentId}",
                WorkflowDocumentType.AssetExpense => $"مصروف أصل رقم {instance.DocumentId}",
                _ => $"مستند رقم {instance.DocumentId}"
            };
        }

        private string GetDescription(WorkflowInstance instance)
        {
            return instance.DocumentType switch
            {
                WorkflowDocumentType.PaymentVoucher => "يرجى مراجعة بيانات سند الدفع واعتمادها",
                WorkflowDocumentType.ReceiptVoucher => "يرجى مراجعة بيانات سند القبض واعتمادها",
                WorkflowDocumentType.DisbursementVoucher => "يرجى مراجعة بيانات سند الصرف واعتمادها",
                WorkflowDocumentType.DynamicScreenEntry => "يرجى مراجعة بيانات الحركة الديناميكية واتخاذ القرار",
                WorkflowDocumentType.AssetExpense => "يرجى مراجعة بيانات مصروف الأصل واعتمادها",
                _ => "يرجى مراجعة المستند واعتماد القرار"
            };
        }
    }
}
