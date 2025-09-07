using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.Controllers
{
    [Authorize]
    public class DisbursementVouchersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public DisbursementVouchersController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var vouchers = await _context.DisbursementVouchers
                .Include(v => v.Account)
                .Include(v => v.Currency)
                .OrderByDescending(v => v.Date)
                .ToListAsync();
            return View(vouchers);
        }

        public async Task<IActionResult> Create()
        {
            ViewBag.Currencies = await _context.Currencies
                .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Code })
                .ToListAsync();
            ViewBag.Accounts = await _context.Accounts
                .Where(a => a.CanPostTransactions)
                .Select(a => new { a.Id, a.Code, a.NameAr, a.CurrencyId })
                .ToListAsync();
            return View(new DisbursementVoucher { Date = DateTime.Now });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DisbursementVoucher model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentAccountId == null || user.PaymentBranchId == null)
                return Challenge();

            var account = await _context.Accounts.FindAsync(model.AccountId);
            if (account == null || account.CurrencyId != model.CurrencyId)
                ModelState.AddModelError("CurrencyId", "العملة لا تطابق عملة الحساب");

            if (!ModelState.IsValid)
            {
                ViewBag.Currencies = await _context.Currencies
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Code })
                    .ToListAsync();
                ViewBag.Accounts = await _context.Accounts
                    .Where(a => a.CanPostTransactions)
                    .Select(a => new { a.Id, a.Code, a.NameAr, a.CurrencyId })
                    .ToListAsync();
                return View(model);
            }

            var currency = await _context.Currencies.FindAsync(model.CurrencyId);
            model.ExchangeRate = currency?.ExchangeRate ?? 1m;

            _context.DisbursementVouchers.Add(model);

            var number = await GenerateJournalEntryNumber();
            var entry = new JournalEntry
            {
                Number = number,
                Date = model.Date,
                Description = model.Notes ?? "سند صرف",
                BranchId = user.PaymentBranchId.Value,
                CreatedById = user.Id,
                TotalDebit = model.Amount,
                TotalCredit = model.Amount,
                Status = JournalEntryStatus.Posted
            };
            entry.Lines.Add(new JournalEntryLine { AccountId = model.AccountId, DebitAmount = model.Amount });
            entry.Lines.Add(new JournalEntryLine { AccountId = user.PaymentAccountId.Value, CreditAmount = model.Amount });

            _context.JournalEntries.Add(entry);
            await UpdateAccountBalances(entry);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        private async Task<string> GenerateJournalEntryNumber()
        {
            var year = DateTime.Now.Year;
            var lastEntry = await _context.JournalEntries
                .Where(j => j.Date.Year == year)
                .OrderByDescending(j => j.Number)
                .FirstOrDefaultAsync();

            if (lastEntry == null)
                return $"JE{year}001";

            var lastNumber = lastEntry.Number.Substring(6);
            if (int.TryParse(lastNumber, out int number))
                return $"JE{year}{(number + 1):D3}";

            return $"JE{year}001";
        }

        private async Task UpdateAccountBalances(JournalEntry entry)
        {
            foreach (var line in entry.Lines)
            {
                var account = await _context.Accounts.FindAsync(line.AccountId);
                if (account == null) continue;

                var netAmount = account.Nature == AccountNature.Debit
                    ? line.DebitAmount - line.CreditAmount
                    : line.CreditAmount - line.DebitAmount;

                account.CurrentBalance += netAmount;
                account.UpdatedAt = DateTime.Now;
            }
        }
    }
}
