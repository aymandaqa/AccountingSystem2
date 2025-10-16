using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using ClosedXML.Excel;
using System.Collections.Generic;
using System.IO;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "disbursementvouchers.view")]
    public class DisbursementVouchersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IJournalEntryService _journalEntryService;

        public DisbursementVouchersController(ApplicationDbContext context, UserManager<User> userManager, IJournalEntryService journalEntryService)
        {
            _context = context;
            _userManager = userManager;
            _journalEntryService = journalEntryService;
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

        [Authorize(Policy = "disbursementvouchers.create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.Suppliers = await _context.Suppliers
                .Include(s => s.Account).ThenInclude(a => a.Currency)
                .Select(s => new { s.Id, s.NameAr, CurrencyId = s.Account!.CurrencyId, CurrencyCode = s.Account.Currency.Code })
                .ToListAsync();
            return View(new DisbursementVoucher { Date = DateTime.Now });
        }

        [HttpPost]
        [Authorize(Policy = "disbursementvouchers.create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DisbursementVoucher model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentAccountId == null || user.PaymentBranchId == null)
                return Challenge();

            var supplier = await _context.Suppliers
                .Include(s => s.Account)
                .FirstOrDefaultAsync(s => s.Id == model.SupplierId);
            if (supplier?.Account == null)
                ModelState.AddModelError("SupplierId", "المورد غير موجود");
            else
            {
                model.AccountId = supplier.AccountId!.Value;
                model.CurrencyId = supplier.Account.CurrencyId;
            }

            var paymentAccount = await _context.Accounts.FindAsync(user.PaymentAccountId);
            if (supplier?.Account != null && paymentAccount != null)
            {
                if (paymentAccount.CurrencyId != supplier.Account.CurrencyId)
                    ModelState.AddModelError("SupplierId", "يجب أن تكون الحسابات بنفس العملة");

                if (paymentAccount.Nature == AccountNature.Debit && model.Amount > paymentAccount.CurrentBalance)
                    ModelState.AddModelError(nameof(model.Amount), "الرصيد المتاح في حساب الدفع لا يكفي لإتمام العملية.");
            }

            ModelState.Remove(nameof(DisbursementVoucher.Account));
            ModelState.Remove(nameof(DisbursementVoucher.CreatedBy));
            ModelState.Remove(nameof(DisbursementVoucher.Supplier));
            ModelState.Remove(nameof(DisbursementVoucher.Currency));

            if (!ModelState.IsValid)
            {
                ViewBag.Suppliers = await _context.Suppliers
                    .Include(s => s.Account).ThenInclude(a => a.Currency)
                    .Select(s => new { s.Id, s.NameAr, CurrencyId = s.Account!.CurrencyId, CurrencyCode = s.Account.Currency.Code })
                    .ToListAsync();
                return View(model);
            }

            var currency = await _context.Currencies.FindAsync(model.CurrencyId);
            if (model.ExchangeRate <= 0)
                model.ExchangeRate = currency?.ExchangeRate ?? 1m;

            model.CreatedById = user.Id;
            _context.DisbursementVouchers.Add(model);
            await _context.SaveChangesAsync();

            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine { AccountId = model.AccountId, DebitAmount = model.Amount },
                new JournalEntryLine { AccountId = user.PaymentAccountId.Value, CreditAmount = model.Amount }
            };

            var reference = $"DSBV:{model.Id}";

            await _journalEntryService.CreateJournalEntryAsync(
                model.Date,
                model.Notes ?? "سند صرف",
                user.PaymentBranchId.Value,
                user.Id,
                lines,
                JournalEntryStatus.Posted,
                reference: reference);

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "disbursementvouchers.view")]
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
            var worksheet = workbook.Worksheets.Add("DisbursementVouchers");

            worksheet.Cell(1, 1).Value = "التاريخ";
            worksheet.Cell(1, 2).Value = "المورد";
            worksheet.Cell(1, 3).Value = "العملة";
            worksheet.Cell(1, 4).Value = "سعر الصرف";
            worksheet.Cell(1, 5).Value = "المبلغ";
            worksheet.Cell(1, 6).Value = "الفرع";
            worksheet.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var voucher in vouchers)
            {
                worksheet.Cell(row, 1).Value = voucher.Date;
                worksheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-MM-dd";
                worksheet.Cell(row, 2).Value = voucher.Supplier?.NameAr ?? string.Empty;
                worksheet.Cell(row, 3).Value = voucher.Currency?.Code ?? string.Empty;
                worksheet.Cell(row, 4).Value = voucher.ExchangeRate;
                worksheet.Cell(row, 5).Value = voucher.Amount;
                worksheet.Cell(row, 6).Value = voucher.CreatedBy?.PaymentBranch?.NameAr ?? string.Empty;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"DisbursementVouchers_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpPost]
        [Authorize(Policy = "disbursementvouchers.delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, DateTime? fromDate = null, DateTime? toDate = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var voucher = await _context.DisbursementVouchers
                .Include(v => v.CreatedBy)
                    .ThenInclude(u => u.PaymentBranch)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (voucher == null)
                return NotFound();

            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == user.Id)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            if (!CanAccessVoucher(user, voucher.CreatedBy, userBranchIds))
                return Forbid();

            var journalEntries = await _context.JournalEntries
                .Include(j => j.Lines)
                    .ThenInclude(l => l.Account)
                .Where(j => j.Reference == $"DSBV:{voucher.Id}")
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
            _context.DisbursementVouchers.Remove(voucher);

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index), new { fromDate, toDate });
        }

        private IQueryable<DisbursementVoucher> BuildQuery(User user, List<int> userBranchIds, DateTime? fromDate, DateTime? toDate)
        {
            var vouchersQuery = _context.DisbursementVouchers
                .Include(v => v.Supplier)
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
