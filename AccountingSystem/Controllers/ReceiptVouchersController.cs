using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using System.Collections.Generic;
using System.Linq;

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
            if (user == null)
                return Challenge();

            var vouchersQuery = _context.ReceiptVouchers
                .Include(v => v.Account)
                .Include(v => v.PaymentAccount)
                .Include(v => v.Currency)
                .Include(v => v.Supplier)
                .Include(v => v.CreatedBy)
                .AsQueryable();

            if (user.PaymentBranchId.HasValue)
            {
                vouchersQuery = vouchersQuery
                    .Where(v => v.CreatedBy.PaymentBranchId == user.PaymentBranchId);
            }
            else
            {
                vouchersQuery = vouchersQuery
                    .Where(v => v.CreatedById == user.Id);
            }

            var vouchers = await vouchersQuery
                .OrderByDescending(v => v.Date)
                .ToListAsync();

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
            _context.ReceiptVouchers.Add(model);
            await _context.SaveChangesAsync();

            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine { AccountId = model.PaymentAccountId, DebitAmount = model.Amount },
                new JournalEntryLine { AccountId = model.AccountId, CreditAmount = model.Amount }
            };

            var reference = $"RCV:{model.Id}";

            await _journalEntryService.CreateJournalEntryAsync(
                model.Date,
                model.Notes ?? "سند قبض",
                user.PaymentBranchId.Value,
                user.Id,
                lines,
                JournalEntryStatus.Posted,
                reference: reference);

            return RedirectToAction(nameof(Index));
        }
    }
}
