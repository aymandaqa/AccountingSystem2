using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.Controllers
{
    [Authorize]
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
                .Where(a => !branchId.HasValue || a.BranchId == branchId)
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
                .Where(a => !branchId.HasValue || a.BranchId == branchId)
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
                .Where(a => !branchId.HasValue || a.BranchId == branchId)
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
