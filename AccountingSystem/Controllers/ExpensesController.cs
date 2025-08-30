using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.AspNetCore.Identity;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "expenses.view")]
    public class ExpensesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public ExpensesController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {

            var brn = await _context.Branches.ToListAsync();
            var expenses = await _context.Expenses
                .Include(e => e.User)
                .Include(e => e.PaymentAccount)
                .Include(e => e.ExpenseAccount)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();

            var model = expenses.Select(e => new ExpenseViewModel
            {
                Id = e.Id,
                UserName = e.User.FullName ?? e.User.Email ?? string.Empty,
                Branch = brn.FirstOrDefault(x => x.Id == e.User.PaymentBranchId)?.NameAr,
                PaymentAccountName = e.PaymentAccount.NameAr,
                ExpenseAccountName = e.ExpenseAccount.NameAr,
                Amount = e.Amount,
                Notes = e.Notes,
                IsApproved = e.IsApproved,
                CreatedAt = e.CreatedAt
            }).ToList();

            return View(model);
        }

        public async Task<IActionResult> My()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var expenses = await _context.Expenses
                .Where(e => e.UserId == userId)
                 .Include(e => e.User)
                .Include(e => e.PaymentAccount)
                .Include(e => e.ExpenseAccount)
                .OrderByDescending(e => e.CreatedAt)
                .ToListAsync();
            var brn = await _context.Branches.ToListAsync();

            var model = expenses.Select(e => new ExpenseViewModel
            {
                Id = e.Id,
                UserName = e.User.FullName ?? e.User.Email ?? string.Empty,
                Branch = brn.FirstOrDefault(x => x.Id == e.User.PaymentBranchId).NameAr,
                PaymentAccountName = e.PaymentAccount.NameAr,
                ExpenseAccountName = e.ExpenseAccount.NameAr,
                Amount = e.Amount,
                Notes = e.Notes,
                IsApproved = e.IsApproved,
                CreatedAt = e.CreatedAt
            }).ToList();

            return View("Index", model);
        }

        [Authorize(Policy = "expenses.create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            var paymentAccount = await _context.Accounts.FindAsync(user.PaymentAccountId);
            var branch = await _context.Branches.FindAsync(user.PaymentBranchId);

            var model = new CreateExpenseViewModel
            {
                PaymentAccountName = paymentAccount?.NameAr ?? string.Empty,
                BranchName = branch?.NameAr ?? string.Empty
            };

            model.ExpenseAccounts = await _context.Accounts
                .Where(a => a.AccountType == AccountType.Expenses && a.CanPostTransactions)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}"
                }).ToListAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "expenses.create")]
        public async Task<IActionResult> Create(CreateExpenseViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null) return Challenge();

            if (!ModelState.IsValid)
            {
                model.PaymentAccountName = (await _context.Accounts.FindAsync(user.PaymentAccountId))?.NameAr ?? string.Empty;
                model.BranchName = (await _context.Branches.FindAsync(user.PaymentBranchId))?.NameAr ?? string.Empty;
                model.ExpenseAccounts = await _context.Accounts
                    .Where(a => a.AccountType == AccountType.Expenses && a.CanPostTransactions)
                    .Select(a => new SelectListItem
                    {
                        Value = a.Id.ToString(),
                        Text = $"{a.Code} - {a.NameAr}"
                    }).ToListAsync();
                return View(model);
            }

            var expense = new Expense
            {
                UserId = user.Id,
                PaymentAccountId = user.PaymentAccountId ?? 0,
                BranchId = user.PaymentBranchId ?? 0,
                ExpenseAccountId = model.ExpenseAccountId,
                Amount = model.Amount,
                Notes = model.Notes,
                CreatedAt = DateTime.UtcNow,
                IsApproved = model.Amount <= user.ExpenseLimit
            };

            if (expense.IsApproved)
            {
                var number = await GenerateJournalEntryNumber();
                var entry = new JournalEntry
                {
                    Number = number,
                    Date = DateTime.Now,
                    Description = model.Notes ?? "مصروف",
                    BranchId = expense.BranchId,
                    CreatedById = user.Id,
                    TotalDebit = expense.Amount,
                    TotalCredit = expense.Amount,
                    Status = JournalEntryStatus.Posted
                };
                entry.Lines.Add(new JournalEntryLine
                {
                    AccountId = expense.ExpenseAccountId,
                    DebitAmount = expense.Amount
                });
                entry.Lines.Add(new JournalEntryLine
                {
                    AccountId = expense.PaymentAccountId,
                    CreditAmount = expense.Amount
                });

                _context.JournalEntries.Add(entry);
                await UpdateAccountBalances(entry);
                await _context.SaveChangesAsync();
                expense.JournalEntryId = entry.Id;
            }

            _context.Expenses.Add(expense);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "expenses.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var expense = await _context.Expenses
                .Include(e => e.PaymentAccount)
                .Include(e => e.Branch)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (expense == null || expense.IsApproved)
                return NotFound();

            var model = new EditExpenseViewModel
            {
                Id = expense.Id,
                ExpenseAccountId = expense.ExpenseAccountId,
                Amount = expense.Amount,
                Notes = expense.Notes,
                PaymentAccountName = expense.PaymentAccount.NameAr,
                BranchName = expense.Branch.NameAr
            };

            model.ExpenseAccounts = await _context.Accounts
                .Where(a => a.AccountType == AccountType.Expenses && a.CanPostTransactions)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}"
                }).ToListAsync();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "expenses.edit")]
        public async Task<IActionResult> Edit(EditExpenseViewModel model)
        {
            var expense = await _context.Expenses.FindAsync(model.Id);
            var user = await _userManager.GetUserAsync(User);
            if (expense == null || expense.IsApproved || user == null)
                return NotFound();

            if (!ModelState.IsValid)
            {
                model.PaymentAccountName = (await _context.Accounts.FindAsync(expense.PaymentAccountId))?.NameAr ?? string.Empty;
                model.BranchName = (await _context.Branches.FindAsync(expense.BranchId))?.NameAr ?? string.Empty;
                model.ExpenseAccounts = await _context.Accounts
                    .Where(a => a.AccountType == AccountType.Expenses && a.CanPostTransactions)
                    .Select(a => new SelectListItem
                    {
                        Value = a.Id.ToString(),
                        Text = $"{a.Code} - {a.NameAr}"
                    }).ToListAsync();
                return View(model);
            }

            expense.ExpenseAccountId = model.ExpenseAccountId;
            expense.Amount = model.Amount;
            expense.Notes = model.Notes;
            expense.IsApproved = model.Amount <= user.ExpenseLimit;

            if (expense.IsApproved && expense.JournalEntryId == null)
            {
                var number = await GenerateJournalEntryNumber();
                var entry = new JournalEntry
                {
                    Number = number,
                    Date = DateTime.Now,
                    Description = expense.Notes ?? "مصروف",
                    BranchId = expense.BranchId,
                    CreatedById = user.Id,
                    TotalDebit = expense.Amount,
                    TotalCredit = expense.Amount,
                    Status = JournalEntryStatus.Posted
                };
                entry.Lines.Add(new JournalEntryLine
                {
                    AccountId = expense.ExpenseAccountId,
                    DebitAmount = expense.Amount,
                    Description = expense.Notes ?? "مصروف",
                });
                entry.Lines.Add(new JournalEntryLine
                {
                    AccountId = expense.PaymentAccountId,
                    CreditAmount = expense.Amount,
                    Description = expense.Notes ?? "مصروف",
                });

                _context.JournalEntries.Add(entry);
                await UpdateAccountBalances(entry);
                await _context.SaveChangesAsync();
                expense.JournalEntryId = entry.Id;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "expenses.delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            if (expense == null || expense.IsApproved)
                return NotFound();

            _context.Expenses.Remove(expense);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "expenses.approve")]
        public async Task<IActionResult> Approve(int id)
        {
            var expense = await _context.Expenses
                .Include(e => e.User)
                .Include(e => e.PaymentAccount)
                .Include(e => e.ExpenseAccount)
                .FirstOrDefaultAsync(e => e.Id == id);
            if (expense == null || expense.IsApproved)
                return NotFound();

            var model = new ExpenseViewModel
            {
                Id = expense.Id,
                UserName = expense.User.FullName ?? expense.User.Email ?? string.Empty,
                PaymentAccountName = expense.PaymentAccount.NameAr,
                ExpenseAccountName = expense.ExpenseAccount.NameAr,
                Amount = expense.Amount,
                Notes = expense.Notes,
                IsApproved = expense.IsApproved,
                CreatedAt = expense.CreatedAt
            };

            return View(model);
        }

        [HttpPost, ActionName("Approve")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "expenses.approve")]
        public async Task<IActionResult> ApproveConfirmed(int id)
        {
            var expense = await _context.Expenses.FindAsync(id);
            var user = await _userManager.GetUserAsync(User);
            if (expense == null || expense.IsApproved || user == null)
                return NotFound();

            var number = await GenerateJournalEntryNumber();
            var entry = new JournalEntry
            {
                Number = number,
                Date = DateTime.Now,
                Description = expense.Notes ?? "مصروف",
                BranchId = expense.BranchId,
                CreatedById = user.Id,
                TotalDebit = expense.Amount,
                TotalCredit = expense.Amount,
                Status = JournalEntryStatus.Posted
            };
            entry.Lines.Add(new JournalEntryLine
            {
                AccountId = expense.ExpenseAccountId,
                DebitAmount = expense.Amount,
                Description = expense.Notes ?? "مصروف",
            });
            entry.Lines.Add(new JournalEntryLine
            {
                AccountId = expense.PaymentAccountId,
                CreditAmount = expense.Amount,
                Description = expense.Notes ?? "مصروف",
            });

            _context.JournalEntries.Add(entry);
            await UpdateAccountBalances(entry);
            await _context.SaveChangesAsync();
            expense.IsApproved = true;
            expense.JournalEntryId = entry.Id;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
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
                account.UpdatedAt = DateTime.UtcNow;
            }
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
    }
}
