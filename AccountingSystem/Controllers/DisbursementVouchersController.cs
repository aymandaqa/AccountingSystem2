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

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var vouchers = await _context.DisbursementVouchers
                .Where(v => v.CreatedById == user!.Id)
                .Include(v => v.Supplier)
                .Include(v => v.Currency)
                .OrderByDescending(v => v.Date)
                .ToListAsync();
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
            if (supplier?.Account != null && paymentAccount != null && paymentAccount.CurrencyId != supplier.Account.CurrencyId)
                ModelState.AddModelError("SupplierId", "يجب أن تكون الحسابات بنفس العملة");

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

            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine { AccountId = model.AccountId, DebitAmount = model.Amount },
                new JournalEntryLine { AccountId = user.PaymentAccountId.Value, CreditAmount = model.Amount }
            };

            await _journalEntryService.CreateJournalEntryAsync(
                model.Date,
                model.Notes ?? "سند صرف",
                user.PaymentBranchId.Value,
                user.Id,
                lines,
                JournalEntryStatus.Posted);

            return RedirectToAction(nameof(Index));
        }
    }
}
