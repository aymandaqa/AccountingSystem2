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
    [Authorize(Policy = "paymentvouchers.view")]
    public class PaymentVouchersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IJournalEntryService _journalEntryService;

        public PaymentVouchersController(ApplicationDbContext context, UserManager<User> userManager, IJournalEntryService journalEntryService)
        {
            _context = context;
            _userManager = userManager;
            _journalEntryService = journalEntryService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            var vouchers = await _context.PaymentVouchers
                .Where(v => v.CreatedById == user!.Id)
                .Include(v => v.Supplier).ThenInclude(s => s.Account)
                .Include(v => v.Currency)
                .OrderByDescending(v => v.Date)
                .ToListAsync();
            return View(vouchers);
        }

        [Authorize(Policy = "paymentvouchers.create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            ViewBag.Suppliers = await _context.Suppliers
                .Include(s => s.Account).ThenInclude(a => a.Currency)
                .Select(s => new { s.Id, s.NameAr, s.AccountId, CurrencyId = s.Account!.CurrencyId, CurrencyCode = s.Account.Currency.Code })
                .ToListAsync();

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

            var supplier = await _context.Suppliers
                .Include(s => s.Account)
                .FirstOrDefaultAsync(s => s.Id == model.SupplierId);
            if (supplier?.Account == null)
                ModelState.AddModelError("SupplierId", "المورد غير موجود");

            Account? selectedAccount = await _context.Accounts.FindAsync(model.AccountId);
            var settingAccount = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "SupplierPaymentsParentAccountId");
            if (selectedAccount == null || settingAccount == null || !int.TryParse(settingAccount.Value, out var parentId) || selectedAccount.ParentId != parentId)
                ModelState.AddModelError("AccountId", "الحساب غير صالح");

            Account? cashAccount = null;
            if (model.IsCash)
            {
                cashAccount = await _context.Accounts.FindAsync(user.PaymentAccountId.Value);
            }

            if (supplier?.Account != null && selectedAccount != null)
            {
                if (supplier.Account.CurrencyId != selectedAccount.CurrencyId)
                    ModelState.AddModelError("SupplierId", "يجب أن تكون الحسابات بنفس العملة");
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
                ViewBag.Suppliers = await _context.Suppliers
                    .Include(s => s.Account).ThenInclude(a => a.Currency)
                    .Select(s => new { s.Id, s.NameAr, s.AccountId, CurrencyId = s.Account!.CurrencyId, CurrencyCode = s.Account.Currency.Code })
                    .ToListAsync();

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

                return View(model);
            }

            var currency = await _context.Currencies.FindAsync(model.CurrencyId);
            model.ExchangeRate = currency?.ExchangeRate ?? 1m;

            model.CreatedById = user.Id;
            _context.PaymentVouchers.Add(model);
            await _context.SaveChangesAsync();

            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine { AccountId =model.AccountId!.Value , DebitAmount = model.Amount },
                new JournalEntryLine { AccountId = supplier.AccountId!.Value, CreditAmount = model.Amount }
            };

            if (model.IsCash)
            {
                lines.Add(new JournalEntryLine { AccountId = supplier.AccountId!.Value, DebitAmount = model.Amount });
                lines.Add(new JournalEntryLine { AccountId = user.PaymentAccountId.Value, CreditAmount = model.Amount });
            }

            var reference = $"PAYV:{model.Id}";

            await _journalEntryService.CreateJournalEntryAsync(
                model.Date,
                model.Notes == null ? "سند دفع" : "سند دفع" + Environment.NewLine + model.Notes,
                user.PaymentBranchId.Value,
                user.Id,
                lines,
                JournalEntryStatus.Posted,
                reference: reference);

            return RedirectToAction(nameof(Index));
        }
    }
}
