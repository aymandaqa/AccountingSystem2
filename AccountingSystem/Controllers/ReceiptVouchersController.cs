using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using System.Collections.Generic;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "receiptvouchers.view")]
    public class ReceiptVouchersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IJournalEntryService _journalEntryService;

        public ReceiptVouchersController(ApplicationDbContext context, UserManager<User> userManager, IJournalEntryService journalEntryService)
        {
            _context = context;
            _userManager = userManager;
            _journalEntryService = journalEntryService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var vouchers = await _context.ReceiptVouchers
                .Where(v => v.CreatedById == user!.Id)
                .Include(v => v.Account)
                .Include(v => v.Currency)
                .OrderByDescending(v => v.Date)
                .ToListAsync();
            return View(vouchers);
        }

        [Authorize(Policy = "receiptvouchers.create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.Accounts = await _context.UserPaymentAccounts
                .Where(u => u.UserId == user!.Id)
                .Include(u => u.Account).ThenInclude(a => a.Currency)
                .Select(u => new { u.Account.Id, u.Account.Code, u.Account.NameAr, u.Account.CurrencyId, CurrencyCode = u.Account.Currency.Code })
                .ToListAsync();
            return View(new ReceiptVoucher { Date = DateTime.Now });
        }

        [HttpPost]
        [Authorize(Policy = "receiptvouchers.create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReceiptVoucher model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentAccountId == null || user.PaymentBranchId == null)
                return Challenge();

            var allowed = await _context.UserPaymentAccounts.AnyAsync(u => u.UserId == user.Id && u.AccountId == model.AccountId);
            var account = await _context.Accounts.FindAsync(model.AccountId);
            if (account == null || !allowed)
                ModelState.AddModelError("AccountId", "الحساب غير موجود");
            else
                model.CurrencyId = account.CurrencyId;

            var paymentAccount = await _context.Accounts.FindAsync(user.PaymentAccountId);
            if (account != null && paymentAccount != null && paymentAccount.CurrencyId != account.CurrencyId)
                ModelState.AddModelError("AccountId", "يجب أن تكون الحسابات بنفس العملة");

            if (!ModelState.IsValid)
            {
                ViewBag.Accounts = await _context.UserPaymentAccounts
                    .Where(u => u.UserId == user.Id)
                    .Include(u => u.Account).ThenInclude(a => a.Currency)
                    .Select(u => new { u.Account.Id, u.Account.Code, u.Account.NameAr, u.Account.CurrencyId, CurrencyCode = u.Account.Currency.Code })
                    .ToListAsync();
                return View(model);
            }

            var currency = await _context.Currencies.FindAsync(model.CurrencyId);
            if (model.ExchangeRate <= 0)
                model.ExchangeRate = currency?.ExchangeRate ?? 1m;

            model.CreatedById = user.Id;
            _context.ReceiptVouchers.Add(model);

            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine { AccountId = user.PaymentAccountId.Value, DebitAmount = model.Amount },
                new JournalEntryLine { AccountId = model.AccountId, CreditAmount = model.Amount }
            };

            await _journalEntryService.CreateJournalEntryAsync(
                model.Date,
                model.Notes ?? "سند قبض",
                user.PaymentBranchId.Value,
                user.Id,
                lines,
                JournalEntryStatus.Posted);

            return RedirectToAction(nameof(Index));
        }
    }
}
