using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "reports.view")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Reports
        public IActionResult Index()
        {
            return View();
        }

        // GET: Reports/TrialBalance
        public async Task<IActionResult> TrialBalance(int? branchId, DateTime? fromDate, DateTime? toDate)
        {
            var accounts = await _context.Accounts
                .Include(a => a.Branch)
                .Where(a => a.CanPostTransactions)
                .Where(a => !branchId.HasValue || a.BranchId == branchId || a.BranchId == null)
                .OrderBy(a => a.Code)
                .ToListAsync();

            var viewModel = new TrialBalanceViewModel
            {
                FromDate = fromDate ?? DateTime.Now.AddMonths(-1),
                ToDate = toDate ?? DateTime.Now,
                BranchId = branchId,
                Accounts = accounts.Select(a => new TrialBalanceAccountViewModel
                {
                    AccountCode = a.Code,
                    AccountName = a.NameAr,
                    DebitBalance = a.Nature == AccountNature.Debit ? a.CurrentBalance : 0,
                    CreditBalance = a.Nature == AccountNature.Credit ? a.CurrentBalance : 0
                }).ToList(),
                Branches = await GetBranchesSelectList()
            };

            viewModel.TotalDebits = viewModel.Accounts.Sum(a => a.DebitBalance);
            viewModel.TotalCredits = viewModel.Accounts.Sum(a => a.CreditBalance);

            return View(viewModel);
        }

        // GET: Reports/BalanceSheet
        public async Task<IActionResult> BalanceSheet(int? branchId, DateTime? asOfDate)
        {
            var accounts = await _context.Accounts
                .Include(a => a.Branch)
                .Where(a => a.CanPostTransactions)
                .Where(a => !branchId.HasValue || a.BranchId == branchId || a.BranchId == null)
                .OrderBy(a => a.Code)
                .ToListAsync();

            var viewModel = new BalanceSheetViewModel
            {
                AsOfDate = asOfDate ?? DateTime.Now,
                BranchId = branchId,
                Assets = accounts.Where(a => a.AccountType == AccountType.Assets)
                    .Select(a => new BalanceSheetItemViewModel
                    {
                        AccountName = a.NameAr,
                        Amount = a.CurrentBalance
                    }).ToList(),
                Liabilities = accounts.Where(a => a.AccountType == AccountType.Liabilities)
                    .Select(a => new BalanceSheetItemViewModel
                    {
                        AccountName = a.NameAr,
                        Amount = a.CurrentBalance
                    }).ToList(),
                Equity = accounts.Where(a => a.AccountType == AccountType.Equity)
                    .Select(a => new BalanceSheetItemViewModel
                    {
                        AccountName = a.NameAr,
                        Amount = a.CurrentBalance
                    }).ToList(),
                Branches = await GetBranchesSelectList()
            };

            viewModel.TotalAssets = viewModel.Assets.Sum(a => a.Amount);
            viewModel.TotalLiabilities = viewModel.Liabilities.Sum(l => l.Amount);
            viewModel.TotalEquity = viewModel.Equity.Sum(e => e.Amount);

            return View(viewModel);
        }

        // GET: Reports/IncomeStatement
        public async Task<IActionResult> IncomeStatement(int? branchId, DateTime? fromDate, DateTime? toDate)
        {
            var accounts = await _context.Accounts
                .Include(a => a.Branch)
                .Where(a => a.CanPostTransactions)
                .Where(a => !branchId.HasValue || a.BranchId == branchId || a.BranchId == null)
                .OrderBy(a => a.Code)
                .ToListAsync();

            var viewModel = new IncomeStatementViewModel
            {
                FromDate = fromDate ?? DateTime.Now.AddMonths(-1),
                ToDate = toDate ?? DateTime.Now,
                BranchId = branchId,
                Revenues = accounts.Where(a => a.AccountType == AccountType.Revenue)
                    .Select(a => new IncomeStatementItemViewModel
                    {
                        AccountName = a.NameAr,
                        Amount = a.CurrentBalance
                    }).ToList(),
                Expenses = accounts.Where(a => a.AccountType == AccountType.Expenses)
                    .Select(a => new IncomeStatementItemViewModel
                    {
                        AccountName = a.NameAr,
                        Amount = a.CurrentBalance
                    }).ToList(),
                Branches = await GetBranchesSelectList()
            };

            viewModel.TotalRevenues = viewModel.Revenues.Sum(r => r.Amount);
            viewModel.TotalExpenses = viewModel.Expenses.Sum(e => e.Amount);
            viewModel.NetIncome = viewModel.TotalRevenues - viewModel.TotalExpenses;

            return View(viewModel);
        }

        // GET: Reports/AccountStatement
        public async Task<IActionResult> AccountStatement(int? accountId, int? branchId, DateTime? fromDate, DateTime? toDate)
        {
            var viewModel = new AccountStatementViewModel
            {
                FromDate = fromDate ?? DateTime.Now.AddMonths(-1),
                ToDate = toDate ?? DateTime.Now,
                BranchId = branchId,
                Accounts = await _context.Accounts
                    .Where(a => a.CanPostTransactions)
                    .Select(a => new SelectListItem
                    {
                        Value = a.Id.ToString(),
                        Text = $"{a.Code} - {a.NameAr}"
                    }).ToListAsync(),
                Branches = await GetBranchesSelectList()
            };

            if (accountId.HasValue)
            {
                var account = await _context.Accounts.FindAsync(accountId.Value);
                if (account != null)
                {
                    viewModel.AccountId = accountId;
                    viewModel.AccountCode = account.Code;
                    viewModel.AccountName = account.NameAr;

                    var lines = await _context.JournalEntryLines
                        .Include(l => l.JournalEntry)
                        .Where(l => l.AccountId == accountId.Value)
                        .Where(l => !branchId.HasValue || l.JournalEntry.BranchId == branchId)
                        .Where(l => l.JournalEntry.Date >= viewModel.FromDate && l.JournalEntry.Date <= viewModel.ToDate)
                        .OrderBy(l => l.JournalEntry.Date)
                        .ThenBy(l => l.JournalEntry.Number)
                        .ToListAsync();

                    decimal running = account.OpeningBalance;
                    foreach (var line in lines)
                    {
                        running += line.DebitAmount - line.CreditAmount;
                        viewModel.Transactions.Add(new AccountTransactionViewModel
                        {
                            Date = line.JournalEntry.Date,
                            JournalEntryNumber = line.JournalEntry.Number,
                            Description = line.Description ?? string.Empty,
                            DebitAmount = line.DebitAmount,
                            CreditAmount = line.CreditAmount,
                            RunningBalance = running
                        });
                    }

                    viewModel.OpeningBalance = account.OpeningBalance;
                    viewModel.ClosingBalance = running;
                    viewModel.TotalDebit = viewModel.Transactions.Sum(t => t.DebitAmount);
                    viewModel.TotalCredit = viewModel.Transactions.Sum(t => t.CreditAmount);
                }
            }

            return View(viewModel);
        }

        // GET: Reports/GeneralLedger
        public async Task<IActionResult> GeneralLedger(int? accountId, int? branchId, DateTime? fromDate, DateTime? toDate)
        {
            var from = fromDate ?? DateTime.Now.AddMonths(-1);
            var to = toDate ?? DateTime.Now;

            var lines = await _context.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Include(l => l.Account)
                .Where(l => l.JournalEntry.Date >= from && l.JournalEntry.Date <= to)
                .Where(l => !branchId.HasValue || l.JournalEntry.BranchId == branchId)
                .Where(l => !accountId.HasValue || l.AccountId == accountId)
                .OrderBy(l => l.Account.Code)
                .ThenBy(l => l.JournalEntry.Date)
                .ToListAsync();

            var accounts = lines
                .GroupBy(l => l.Account)
                .Select(g => new GeneralLedgerAccountViewModel
                {
                    AccountCode = g.Key.Code,
                    AccountName = g.Key.NameAr,
                    Transactions = g.Select(l => new GeneralLedgerTransactionViewModel
                    {
                        Date = l.JournalEntry.Date,
                        JournalEntryNumber = l.JournalEntry.Number,
                        Description = l.Description ?? string.Empty,
                        DebitAmount = l.DebitAmount,
                        CreditAmount = l.CreditAmount
                    }).ToList()
                }).ToList();

            var viewModel = new GeneralLedgerViewModel
            {
                FromDate = from,
                ToDate = to,
                BranchId = branchId,
                AccountId = accountId,
                Accounts = accounts,
                Branches = await GetBranchesSelectList(),
                AccountOptions = await _context.Accounts
                    .Where(a => a.CanPostTransactions)
                    .Select(a => new SelectListItem
                    {
                        Value = a.Id.ToString(),
                        Text = $"{a.Code} - {a.NameAr}"
                    }).ToListAsync()
            };

            return View(viewModel);
        }

        private async Task<List<SelectListItem>> GetBranchesSelectList()
        {
            return await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                }).ToListAsync();
        }
    }
}
