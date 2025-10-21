using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Models.Workflows;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "paymentvouchers.view")]
    public class PaymentVouchersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWorkflowService _workflowService;
        private readonly IPaymentVoucherProcessor _paymentVoucherProcessor;

        public PaymentVouchersController(ApplicationDbContext context, UserManager<User> userManager, IWorkflowService workflowService, IPaymentVoucherProcessor paymentVoucherProcessor)
        {
            _context = context;
            _userManager = userManager;
            _workflowService = workflowService;
            _paymentVoucherProcessor = paymentVoucherProcessor;
        }

        private async Task PopulatePaymentAccountSelectListAsync()
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "SupplierPaymentsParentAccountId");
            if (setting != null && int.TryParse(setting.Value, out var parentAccountId))
            {
                ViewBag.Accounts = await _context.Accounts
                    .Where(a => a.ParentId == parentAccountId)
                    .Include(a => a.Currency)
                    .Select(a => new { a.Id, a.Code, a.NameAr, a.CurrencyId, CurrencyCode = a.Currency.Code })
                    .ToListAsync();
            }
            else
            {
                ViewBag.Accounts = new List<object>();
            }
        }

        private async Task PopulateSupplierSelectListAsync()
        {
            ViewBag.Suppliers = await _context.Suppliers
                .Include(s => s.Account).ThenInclude(a => a.Currency)
                .Where(s => s.AccountId != null)
                .OrderBy(s => s.NameAr)
                .Select(s => new
                {
                    s.Id,
                    s.NameAr,
                    s.AccountId,
                    CurrencyId = s.Account!.CurrencyId,
                    CurrencyCode = s.Account.Currency.Code
                })
                .ToListAsync();
        }

        public async Task<IActionResult> Index(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == user.Id)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            var vouchers = await BuildQuery(user, userBranchIds, fromDate, toDate)
                .OrderByDescending(v => v.Date)
                .ToListAsync();

            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(vouchers);
        }

        [Authorize(Policy = "paymentvouchers.create")]
        public async Task<IActionResult> Create()
        {
            await PopulateSupplierSelectListAsync();
            await PopulatePaymentAccountSelectListAsync();

            return View(new PaymentVoucher { Date = DateTime.Now, IsCash = true });
        }

        [HttpPost]
        [Authorize(Policy = "paymentvouchers.create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PaymentVoucher model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentAccountId == null || user.PaymentBranchId == null)
                return Challenge();

            if (!model.SupplierId.HasValue)
            {
                ModelState.AddModelError(nameof(PaymentVoucher.SupplierId), "الرجاء اختيار المورد");
            }

            Supplier? supplier = null;
            if (model.SupplierId.HasValue)
            {
                supplier = await _context.Suppliers
                    .Include(s => s.Account)
                    .FirstOrDefaultAsync(s => s.Id == model.SupplierId.Value);
                if (supplier?.Account == null)
                    ModelState.AddModelError(nameof(PaymentVoucher.SupplierId), "المورد غير موجود");
            }

            Account? selectedAccount = await _context.Accounts.FindAsync(model.AccountId);
            var settingAccount = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "SupplierPaymentsParentAccountId");
            if (selectedAccount == null || settingAccount == null || !int.TryParse(settingAccount.Value, out var parentId) || selectedAccount.ParentId != parentId)
                ModelState.AddModelError("AccountId", "الحساب غير صالح");

            Account? cashAccount = null;
            if (model.IsCash)
            {
                cashAccount = await _context.Accounts.FindAsync(user.PaymentAccountId.Value);
                if (cashAccount != null && cashAccount.Nature == AccountNature.Debit && model.Amount > cashAccount.CurrentBalance)
                    ModelState.AddModelError(nameof(model.Amount), "الرصيد المتاح في حساب الدفع لا يكفي لإتمام العملية.");
            }

            if (supplier?.Account != null && selectedAccount != null)
            {
                if (supplier.Account.CurrencyId != selectedAccount.CurrencyId)
                    ModelState.AddModelError(nameof(PaymentVoucher.SupplierId), "يجب أن تكون الحسابات بنفس العملة");
                if (model.IsCash && cashAccount != null && selectedAccount.CurrencyId != cashAccount.CurrencyId)
                    ModelState.AddModelError("AccountId", "يجب أن تكون الحسابات بنفس العملة");
            }

            if (supplier?.Account != null)
            {
                model.CurrencyId = supplier.Account.CurrencyId;
            }

            ModelState.Remove(nameof(PaymentVoucher.CurrencyId));
            ModelState.Remove(nameof(PaymentVoucher.ExchangeRate));

            if (!ModelState.IsValid)
            {
                await PopulateSupplierSelectListAsync();
                await PopulatePaymentAccountSelectListAsync();
                return View(model);
            }

            return await FinalizeCreationAsync(model, user);
        }
        [Authorize(Policy = "paymentvouchers.create")]
        public async Task<IActionResult> CreateFromAgent()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            if (user.AgentId == null)
            {
                TempData["ErrorMessage"] = "لم يتم ربط المستخدم بأي وكيل.";
                return RedirectToAction(nameof(Index));
            }

            if (user.PaymentAccountId == null || user.PaymentBranchId == null)
            {
                TempData["ErrorMessage"] = "لم يتم إعداد حساب الدفع للمستخدم.";
                return RedirectToAction(nameof(Index));
            }

            var agent = await _context.Agents
                .Include(a => a.Account).ThenInclude(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == user.AgentId.Value);

            if (agent?.Account == null)
            {
                TempData["ErrorMessage"] = "لا يملك الوكيل المحدد حساباً مالياً.";
                return RedirectToAction(nameof(Index));
            }

            var paymentAccount = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == user.PaymentAccountId.Value);

            if (paymentAccount == null)
            {
                TempData["ErrorMessage"] = "حساب الدفع المحدد للمستخدم غير موجود.";
                return RedirectToAction(nameof(Index));
            }

            if (paymentAccount.CurrencyId != agent.Account.CurrencyId)
            {
                TempData["ErrorMessage"] = "عملة حساب الدفع لا تطابق عملة حساب الوكيل.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Agent = new { agent.Id, agent.Name };
            ViewBag.PaymentAccount = new
            {
                paymentAccount.Id,
                paymentAccount.Code,
                paymentAccount.NameAr
            };
            ViewBag.CurrencyCode = agent.Account.Currency.Code;

            var model = new PaymentVoucher
            {
                Date = DateTime.Now,
                AgentId = agent.Id,
                AccountId = paymentAccount.Id,
                CurrencyId = agent.Account.CurrencyId,
                IsCash = false
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Policy = "paymentvouchers.create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromAgent(PaymentVoucher model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentAccountId == null || user.PaymentBranchId == null)
                return Challenge();

            ModelState.Remove(nameof(PaymentVoucher.SupplierId));
            ModelState.Remove(nameof(PaymentVoucher.AccountId));
            ModelState.Remove(nameof(PaymentVoucher.AgentId));
            ModelState.Remove(nameof(PaymentVoucher.CurrencyId));
            ModelState.Remove(nameof(PaymentVoucher.ExchangeRate));

            if (user.AgentId == null)
            {
                ModelState.AddModelError(string.Empty, "لم يتم ربط المستخدم بأي وكيل.");
            }

            var agent = user.AgentId.HasValue
                ? await _context.Agents
                    .Include(a => a.Account).ThenInclude(a => a.Currency)
                    .FirstOrDefaultAsync(a => a.Id == user.AgentId.Value)
                : null;

            if (agent?.Account == null)
            {
                ModelState.AddModelError(nameof(PaymentVoucher.AgentId), "لا يملك الوكيل المحدد حساباً مالياً.");
            }

            var paymentAccount = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == user.PaymentAccountId.Value);

            if (paymentAccount == null)
            {
                ModelState.AddModelError(nameof(PaymentVoucher.AccountId), "حساب الدفع المحدد للمستخدم غير موجود.");
            }

            if (agent?.Account != null && paymentAccount != null && agent.Account.CurrencyId != paymentAccount.CurrencyId)
            {
                ModelState.AddModelError(nameof(PaymentVoucher.AgentId), "عملة حساب الوكيل لا تطابق عملة حساب الدفع.");
            }

            if (agent?.Account != null && model.Amount > agent.Account.CurrentBalance)
            {
                ModelState.AddModelError(nameof(PaymentVoucher.Amount), "الرصيد المتاح في حساب الوكيل لا يكفي لإتمام العملية.");
            }

            if (agent?.Account != null && paymentAccount != null)
            {
                model.AgentId = agent.Id;
                model.AccountId = paymentAccount.Id;
                model.CurrencyId = agent.Account.CurrencyId;
                model.SupplierId = null;
                model.IsCash = false;
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Agent = agent != null ? new { agent.Id, agent.Name } : null;
                ViewBag.PaymentAccount = paymentAccount != null ? new { paymentAccount.Id, paymentAccount.Code, paymentAccount.NameAr } : null;
                ViewBag.CurrencyCode = agent?.Account?.Currency?.Code;
                return View(model);
            }

            return await FinalizeCreationAsync(model, user);
        }

        [Authorize(Policy = "paymentvouchers.view")]
        public async Task<IActionResult> ExportExcel(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == user.Id)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            var vouchers = await BuildQuery(user, userBranchIds, fromDate, toDate)
                .OrderByDescending(v => v.Date)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("PaymentVouchers");

            worksheet.Cell(1, 1).Value = "التاريخ";
            worksheet.Cell(1, 2).Value = "المورد";
            worksheet.Cell(1, 3).Value = "الوكيل";
            worksheet.Cell(1, 4).Value = "العملة";
            worksheet.Cell(1, 5).Value = "سعر الصرف";
            worksheet.Cell(1, 6).Value = "المبلغ";
            worksheet.Cell(1, 7).Value = "الحالة";
            worksheet.Cell(1, 8).Value = "الفرع";
            worksheet.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var voucher in vouchers)
            {
                worksheet.Cell(row, 1).Value = voucher.Date;
                worksheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-MM-dd";
                worksheet.Cell(row, 2).Value = voucher.Supplier?.NameAr ?? string.Empty;
                worksheet.Cell(row, 3).Value = voucher.Agent?.Name ?? string.Empty;
                worksheet.Cell(row, 4).Value = voucher.Currency?.Code ?? string.Empty;
                worksheet.Cell(row, 5).Value = voucher.ExchangeRate;
                worksheet.Cell(row, 6).Value = voucher.Amount;
                worksheet.Cell(row, 7).Value = voucher.Status.ToString();
                worksheet.Cell(row, 8).Value = voucher.CreatedBy?.PaymentBranch?.NameAr ?? string.Empty;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"PaymentVouchers_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpPost]
        [Authorize(Policy = "paymentvouchers.delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var voucher = await _context.PaymentVouchers
                .Include(v => v.CreatedBy)
                    .ThenInclude(u => u.PaymentBranch)
                .Include(v => v.WorkflowInstance)
                    .ThenInclude(i => i!.Actions)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (voucher == null)
                return NotFound();

            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == user.Id)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            if (!CanAccessVoucher(user, voucher.CreatedBy, userBranchIds))
                return Forbid();

            var journalReferences = new[]
            {
                $"سند مصاريف:{voucher.Id}",
                $"سند دفع وكيل:{voucher.Id}"
            };

            var journalEntries = await _context.JournalEntries
                .Include(j => j.Lines)
                    .ThenInclude(l => l.Account)
                .Where(j => journalReferences.Contains(j.Reference!))
                .ToListAsync();

            foreach (var entry in journalEntries.Where(e => e.Status == JournalEntryStatus.Posted))
            {
                foreach (var line in entry.Lines)
                {
                    var account = line.Account;
                    var netAmount = account.Nature == AccountNature.Debit
                        ? line.DebitAmount - line.CreditAmount
                        : line.CreditAmount - line.DebitAmount;

                    account.CurrentBalance -= netAmount;
                    account.UpdatedAt = DateTime.Now;
                }
            }

            if (voucher.WorkflowInstance != null)
            {
                _context.WorkflowActions.RemoveRange(voucher.WorkflowInstance.Actions);
                _context.WorkflowInstances.Remove(voucher.WorkflowInstance);
            }

            _context.JournalEntryLines.RemoveRange(journalEntries.SelectMany(j => j.Lines));
            _context.JournalEntries.RemoveRange(journalEntries);
            _context.PaymentVouchers.Remove(voucher);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { fromDate, toDate });
        }

        private async Task<IActionResult> FinalizeCreationAsync(PaymentVoucher model, User user)
        {
            var currency = await _context.Currencies.FindAsync(model.CurrencyId);
            model.ExchangeRate = currency?.ExchangeRate ?? 1m;

            model.CreatedById = user.Id;
            var definition = await _workflowService.GetActiveDefinitionAsync(WorkflowDocumentType.PaymentVoucher, user.PaymentBranchId);
            model.Status = definition != null ? PaymentVoucherStatus.PendingApproval : PaymentVoucherStatus.Approved;

            _context.PaymentVouchers.Add(model);
            await _context.SaveChangesAsync();

            if (definition != null)
            {
                var baseAmount = model.Amount * model.ExchangeRate;
                var instance = await _workflowService.StartWorkflowAsync(
                    definition,
                    WorkflowDocumentType.PaymentVoucher,
                    model.Id,
                    user.Id,
                    user.PaymentBranchId,
                    model.Amount,
                    baseAmount,
                    model.CurrencyId);
                if (instance != null)
                {
                    model.WorkflowInstanceId = instance.Id;
                    await _context.SaveChangesAsync();
                }

                TempData["InfoMessage"] = "تم إرسال سند الدفع لاعتمادات الموافقة";
            }
            else
            {
                await _paymentVoucherProcessor.FinalizeVoucherAsync(model, user.Id);
                TempData["SuccessMessage"] = "تم إنشاء سند الدفع واعتماده فوراً";
            }

            return RedirectToAction(nameof(Index));
        }

        private IQueryable<PaymentVoucher> BuildQuery(User user, List<int> userBranchIds, DateTime? fromDate, DateTime? toDate)
        {
            var vouchersQuery = _context.PaymentVouchers
                .Include(v => v.Supplier).ThenInclude(s => s.Account)
                .Include(v => v.Agent).ThenInclude(a => a.Account)
                .Include(v => v.Currency)
                .Include(v => v.CreatedBy)
                    .ThenInclude(u => u.PaymentBranch)
                .AsQueryable();

            if (userBranchIds.Any())
            {
                vouchersQuery = vouchersQuery
                    .Where(v => v.CreatedBy.PaymentBranchId.HasValue && userBranchIds.Contains(v.CreatedBy.PaymentBranchId.Value));
            }
            else if (user.PaymentBranchId.HasValue)
            {
                vouchersQuery = vouchersQuery
                    .Where(v => v.CreatedBy.PaymentBranchId == user.PaymentBranchId);
            }
            else
            {
                vouchersQuery = vouchersQuery
                    .Where(v => v.CreatedById == user.Id);
            }

            if (fromDate.HasValue)
            {
                var startDate = fromDate.Value.Date;
                vouchersQuery = vouchersQuery.Where(v => v.Date >= startDate);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1);
                vouchersQuery = vouchersQuery.Where(v => v.Date < endDate);
            }

            return vouchersQuery;
        }

        private static bool CanAccessVoucher(User currentUser, User createdBy, List<int> userBranchIds)
        {
            if (userBranchIds.Any())
            {
                return createdBy.PaymentBranchId.HasValue && userBranchIds.Contains(createdBy.PaymentBranchId.Value);
            }

            if (currentUser.PaymentBranchId.HasValue)
            {
                return createdBy.PaymentBranchId == currentUser.PaymentBranchId;
            }

            return currentUser.Id == createdBy.Id;
        }
    }
}
