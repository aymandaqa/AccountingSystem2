using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Models.Workflows;
using ClosedXML.Excel;
using System.Collections.Generic;
using System.IO;
using System.Linq;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "receiptvouchers.view")]
    public class ReceiptVouchersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWorkflowService _workflowService;
        private readonly IReceiptVoucherProcessor _receiptVoucherProcessor;

        public ReceiptVouchersController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IWorkflowService workflowService,
            IReceiptVoucherProcessor receiptVoucherProcessor)
        {
            _context = context;
            _userManager = userManager;
            _workflowService = workflowService;
            _receiptVoucherProcessor = receiptVoucherProcessor;
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

            var vouchersQuery = BuildQuery(user, userBranchIds, fromDate, toDate);

            var vouchers = await vouchersQuery
                .OrderByDescending(v => v.Date)
                .ToListAsync();

            ViewBag.FromDate = fromDate?.ToString("yyyy-MM-dd");
            ViewBag.ToDate = toDate?.ToString("yyyy-MM-dd");

            return View(vouchers);
        }

        [Authorize(Policy = "receiptvouchers.create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            var paymentAccounts = await _context.UserPaymentAccounts
                .AsNoTracking()
                .Where(u => u.UserId == user!.Id)
                .Include(u => u.Account).ThenInclude(a => a.Currency)
                .Select(u => new { u.Account.Id, u.Account.Code, u.Account.NameAr, u.Account.CurrencyId, CurrencyCode = u.Account.Currency.Code })
                .ToListAsync();
            ViewBag.PaymentAccounts = paymentAccounts;
            ViewBag.Suppliers = await _context.Suppliers
                .AsNoTracking()
                .Include(s => s.Account).ThenInclude(a => a.Currency)
                .Where(s => s.AccountId != null)
                .Select(s => new
                {
                    s.Id,
                    s.NameAr,
                    AccountId = s.AccountId!.Value,
                    s.Account!.CurrencyId,
                    CurrencyCode = s.Account.Currency.Code
                })
                .ToListAsync();
            var model = new ReceiptVoucher { Date = DateTime.Now };
            var defaultPaymentAccount = user?.PaymentAccountId;
            if (defaultPaymentAccount.HasValue && paymentAccounts.Any(a => a.Id == defaultPaymentAccount.Value))
            {
                model.PaymentAccountId = defaultPaymentAccount.Value;
            }

            return View(model);
        }

        [HttpPost]
        [Authorize(Policy = "receiptvouchers.create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReceiptVoucher model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentBranchId == null)
                return Challenge();

            ModelState.Remove(nameof(ReceiptVoucher.Account));
            ModelState.Remove(nameof(ReceiptVoucher.Currency));
            ModelState.Remove(nameof(ReceiptVoucher.CreatedBy));
            ModelState.Remove(nameof(ReceiptVoucher.Supplier));
            ModelState.Remove(nameof(ReceiptVoucher.PaymentAccount));

            Account? account = null;
            Account? paymentAccount = null;

            if (!model.SupplierId.HasValue)
            {
                ModelState.AddModelError(nameof(ReceiptVoucher.SupplierId), "الرجاء اختيار المورد");
            }

            if (model.SupplierId.HasValue)
            {
                var supplier = await _context.Suppliers
                    .Include(s => s.Account)
                    .ThenInclude(a => a.Currency)
                    .FirstOrDefaultAsync(s => s.Id == model.SupplierId.Value);

                if (supplier?.Account == null)
                {
                    ModelState.AddModelError(nameof(ReceiptVoucher.SupplierId), "المورد غير موجود أو لا يملك حساباً");
                }
                else
                {
                    account = supplier.Account;
                    model.AccountId = supplier.AccountId!.Value;
                    model.CurrencyId = supplier.Account.CurrencyId;
                    ModelState.Remove(nameof(ReceiptVoucher.AccountId));
                    ModelState.Remove(nameof(ReceiptVoucher.CurrencyId));
                }
            }

            if (model.PaymentAccountId != 0)
            {
                var allowedPaymentAccount = await _context.UserPaymentAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == user.Id && u.AccountId == model.PaymentAccountId);

                if (allowedPaymentAccount == null)
                {
                    ModelState.AddModelError(nameof(ReceiptVoucher.PaymentAccountId), "حساب الدفع المحدد غير متاح للمستخدم");
                }
                else
                {
                    paymentAccount = await _context.Accounts.FindAsync(model.PaymentAccountId);
                    if (paymentAccount == null)
                    {
                        ModelState.AddModelError(nameof(ReceiptVoucher.PaymentAccountId), "حساب الدفع غير موجود");
                    }
                }
            }
            else
            {
                ModelState.AddModelError(nameof(ReceiptVoucher.PaymentAccountId), "الرجاء اختيار حساب الدفع");
            }

            if (account != null && paymentAccount != null && paymentAccount.CurrencyId != account.CurrencyId)
            {
                ModelState.AddModelError(nameof(ReceiptVoucher.PaymentAccountId), "يجب أن تكون الحسابات بنفس العملة");
            }

            if (!ModelState.IsValid)
            {
                ViewBag.PaymentAccounts = await _context.UserPaymentAccounts
                    .AsNoTracking()
                    .Where(u => u.UserId == user.Id)
                    .Include(u => u.Account).ThenInclude(a => a.Currency)
                    .Select(u => new { u.Account.Id, u.Account.Code, u.Account.NameAr, u.Account.CurrencyId, CurrencyCode = u.Account.Currency.Code })
                    .ToListAsync();
                ViewBag.Suppliers = await _context.Suppliers
                    .AsNoTracking()
                    .Include(s => s.Account).ThenInclude(a => a.Currency)
                    .Where(s => s.AccountId != null)
                    .Select(s => new
                    {
                        s.Id,
                        s.NameAr,
                        AccountId = s.AccountId!.Value,
                        s.Account!.CurrencyId,
                        CurrencyCode = s.Account.Currency.Code
                    })
                    .ToListAsync();
                return View(model);
            }

            var currency = await _context.Currencies.FindAsync(model.CurrencyId);
            if (model.ExchangeRate <= 0)
                model.ExchangeRate = currency?.ExchangeRate ?? 1m;

            model.CreatedById = user.Id;
            var definition = await _workflowService.GetActiveDefinitionAsync(WorkflowDocumentType.ReceiptVoucher, user.PaymentBranchId);
            model.Status = definition != null ? ReceiptVoucherStatus.PendingApproval : ReceiptVoucherStatus.Approved;

            _context.ReceiptVouchers.Add(model);
            await _context.SaveChangesAsync();

            var baseAmount = model.Amount * model.ExchangeRate;

            if (definition != null)
            {
                var instance = await _workflowService.StartWorkflowAsync(
                    definition,
                    WorkflowDocumentType.ReceiptVoucher,
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
                    TempData["InfoMessage"] = "تم إرسال سند القبض لاعتمادات الموافقة";
                }
                else
                {
                    await _receiptVoucherProcessor.FinalizeAsync(model, user.Id);
                    TempData["SuccessMessage"] = "تم إنشاء سند القبض واعتماده فوراً";
                }
            }
            else
            {
                await _receiptVoucherProcessor.FinalizeAsync(model, user.Id);
                TempData["SuccessMessage"] = "تم إنشاء سند القبض واعتماده فوراً";
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "receiptvouchers.view")]
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
            var worksheet = workbook.Worksheets.Add("ReceiptVouchers");

            worksheet.Cell(1, 1).Value = "التاريخ";
            worksheet.Cell(1, 2).Value = "المورد";
            worksheet.Cell(1, 3).Value = "حساب الدفع";
            worksheet.Cell(1, 4).Value = "حساب المورد";
            worksheet.Cell(1, 5).Value = "العملة";
            worksheet.Cell(1, 6).Value = "سعر الصرف";
            worksheet.Cell(1, 7).Value = "المبلغ";
            worksheet.Cell(1, 8).Value = "المبلغ بالعملة الأساسية";
            worksheet.Cell(1, 9).Value = "الحالة";
            worksheet.Cell(1, 10).Value = "الفرع";
            worksheet.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var voucher in vouchers)
            {
                worksheet.Cell(row, 1).Value = voucher.Date;
                worksheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-MM-dd";
                worksheet.Cell(row, 2).Value = voucher.Supplier?.NameAr ?? string.Empty;
                worksheet.Cell(row, 3).Value = voucher.PaymentAccount?.NameAr ?? string.Empty;
                worksheet.Cell(row, 4).Value = voucher.Account?.NameAr ?? string.Empty;
                worksheet.Cell(row, 5).Value = voucher.Currency?.Code ?? string.Empty;
                worksheet.Cell(row, 6).Value = voucher.ExchangeRate;
                worksheet.Cell(row, 7).Value = voucher.Amount;
                worksheet.Cell(row, 8).Value = voucher.Amount * voucher.ExchangeRate;
                worksheet.Cell(row, 9).Value = voucher.Status.ToString();
                worksheet.Cell(row, 10).Value = voucher.CreatedBy?.PaymentBranch?.NameAr ?? string.Empty;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"ReceiptVouchers_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpPost]
        [Authorize(Policy = "receiptvouchers.delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var voucher = await _context.ReceiptVouchers
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

            if (voucher.Status == ReceiptVoucherStatus.Approved)
            {
                var journalEntries = await _context.JournalEntries
                    .Include(j => j.Lines)
                        .ThenInclude(l => l.Account)
                    .Where(j => j.Reference == $"RCV:{voucher.Id}")
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

                _context.JournalEntryLines.RemoveRange(journalEntries.SelectMany(j => j.Lines));
                _context.JournalEntries.RemoveRange(journalEntries);
            }

            if (voucher.WorkflowInstance != null)
            {
                _context.WorkflowActions.RemoveRange(voucher.WorkflowInstance.Actions);
                _context.WorkflowInstances.Remove(voucher.WorkflowInstance);
            }

            _context.ReceiptVouchers.Remove(voucher);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { fromDate, toDate });
        }

        private IQueryable<ReceiptVoucher> BuildQuery(User user, List<int> userBranchIds, DateTime? fromDate, DateTime? toDate)
        {
            var vouchersQuery = _context.ReceiptVouchers
                .Include(v => v.Account)
                .Include(v => v.PaymentAccount)
                .Include(v => v.Currency)
                .Include(v => v.Supplier)
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
