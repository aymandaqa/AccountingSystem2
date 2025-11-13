using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.Workflows;
using AccountingSystem.Services;
using AccountingSystem.ViewModels.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "workflowapprovals.view")]
    public class WorkflowApprovalsController : Controller
    {
        private readonly IWorkflowService _workflowService;
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IPaymentVoucherProcessor _paymentVoucherProcessor;
        private readonly IReceiptVoucherProcessor _receiptVoucherProcessor;
        private readonly IDisbursementVoucherProcessor _disbursementVoucherProcessor;
        private readonly IAssetExpenseProcessor _assetExpenseProcessor;

        public WorkflowApprovalsController(
            IWorkflowService workflowService,
            ApplicationDbContext context,
            UserManager<User> userManager,
            IPaymentVoucherProcessor paymentVoucherProcessor,
            IReceiptVoucherProcessor receiptVoucherProcessor,
            IDisbursementVoucherProcessor disbursementVoucherProcessor,
            IAssetExpenseProcessor assetExpenseProcessor)
        {
            _workflowService = workflowService;
            _context = context;
            _userManager = userManager;
            _paymentVoucherProcessor = paymentVoucherProcessor;
            _receiptVoucherProcessor = receiptVoucherProcessor;
            _disbursementVoucherProcessor = disbursementVoucherProcessor;
            _assetExpenseProcessor = assetExpenseProcessor;
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
                .Include(e => e.Asset).ThenInclude(a => a.CostCenter)
                .Include(e => e.ExpenseAccount)
                .Include(e => e.Supplier)
                .Include(e => e.Currency)
                .Include(e => e.CreatedBy)
                .Where(e => assetExpenseIds.Contains(e.Id))
                .ToDictionaryAsync(e => e.Id);

            var journalPreviewCache = new Dictionary<(WorkflowDocumentType, int), List<WorkflowJournalEntryLineViewModel>>();
            var journalPreviewErrors = new Dictionary<(WorkflowDocumentType, int), string>();

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
                    AppendAttachment(model, voucher.AttachmentFilePath, voucher.AttachmentFileName);
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
                    AppendAttachment(model, receipt.AttachmentFilePath, receipt.AttachmentFileName);
                }
                else if (action.WorkflowInstance.DocumentType == WorkflowDocumentType.DisbursementVoucher && disbursementVouchers.TryGetValue(action.WorkflowInstance.DocumentId, out var disbursement))
                {
                    model.DisbursementVoucher = disbursement;
                    if (string.IsNullOrEmpty(model.CurrencyCode))
                    {
                        model.CurrencyCode = disbursement.Currency?.Code;
                    }
                    AppendAttachment(model, disbursement.AttachmentFilePath, disbursement.AttachmentFileName);
                }
                else if (action.WorkflowInstance.DocumentType == WorkflowDocumentType.AssetExpense && assetExpenses.TryGetValue(action.WorkflowInstance.DocumentId, out var assetExpense))
                {
                    model.AssetExpense = assetExpense;
                    if (string.IsNullOrEmpty(model.CurrencyCode))
                    {
                        model.CurrencyCode = assetExpense.Currency?.Code;
                    }
                    AppendAttachment(model, assetExpense.AttachmentFilePath, assetExpense.AttachmentFileName);
                }

                var cacheKey = (action.WorkflowInstance.DocumentType, action.WorkflowInstance.DocumentId);

                if (journalPreviewCache.TryGetValue(cacheKey, out var cachedLines))
                {
                    model.JournalLines = CloneLines(cachedLines);
                }
                else if (journalPreviewErrors.TryGetValue(cacheKey, out var cachedError))
                {
                    model.JournalPreviewError = cachedError;
                }
                else
                {
                    try
                    {
                        JournalEntryPreview? preview = action.WorkflowInstance.DocumentType switch
                        {
                            WorkflowDocumentType.PaymentVoucher => await _paymentVoucherProcessor.BuildPreviewAsync(action.WorkflowInstance.DocumentId),
                            WorkflowDocumentType.ReceiptVoucher => await _receiptVoucherProcessor.BuildPreviewAsync(action.WorkflowInstance.DocumentId),
                            WorkflowDocumentType.DisbursementVoucher => await _disbursementVoucherProcessor.BuildPreviewAsync(action.WorkflowInstance.DocumentId),
                            WorkflowDocumentType.AssetExpense => await _assetExpenseProcessor.BuildPreviewAsync(action.WorkflowInstance.DocumentId),
                            _ => null
                        };

                        if (preview != null)
                        {
                            var lines = MapJournalLines(preview);
                            model.JournalLines = CloneLines(lines);
                            journalPreviewCache[cacheKey] = lines;
                        }
                    }
                    catch (Exception ex)
                    {
                        model.JournalPreviewError = ex.Message;
                        journalPreviewErrors[cacheKey] = ex.Message;
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

        private static void AppendAttachment(WorkflowApprovalViewModel model, string? path, string? name)
        {
            if (string.IsNullOrWhiteSpace(path))
            {
                return;
            }

            model.Attachments.Add(new WorkflowAttachmentViewModel
            {
                FilePath = path,
                FileName = string.IsNullOrWhiteSpace(name) ? "عرض المرفق" : name
            });
        }

        private static List<WorkflowJournalEntryLineViewModel> MapJournalLines(JournalEntryPreview preview)
        {
            return preview.Lines.Select(line => new WorkflowJournalEntryLineViewModel
            {
                AccountCode = line.Account?.Code ?? string.Empty,
                AccountName = line.Account == null
                    ? string.Empty
                    : string.IsNullOrWhiteSpace(line.Account.NameAr)
                        ? string.IsNullOrWhiteSpace(line.Account.NameEn) ? (line.Account.Code ?? string.Empty) : line.Account.NameEn!
                        : line.Account.NameAr!,
                Debit = line.Debit,
                Credit = line.Credit,
                Description = line.Description,
                CostCenter = line.CostCenter != null
                    ? (string.IsNullOrWhiteSpace(line.CostCenter.NameAr)
                        ? string.IsNullOrWhiteSpace(line.CostCenter.NameEn) ? line.CostCenter.Code : line.CostCenter.NameEn
                        : line.CostCenter.NameAr)
                    : null
            }).ToList();
        }

        private static List<WorkflowJournalEntryLineViewModel> CloneLines(IEnumerable<WorkflowJournalEntryLineViewModel> source)
        {
            return source.Select(line => new WorkflowJournalEntryLineViewModel
            {
                AccountCode = line.AccountCode,
                AccountName = line.AccountName,
                Debit = line.Debit,
                Credit = line.Credit,
                Description = line.Description,
                CostCenter = line.CostCenter
            }).ToList();
        }

        private string GetTitle(WorkflowInstance instance)
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

        private string GetDescription(WorkflowInstance instance)
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
