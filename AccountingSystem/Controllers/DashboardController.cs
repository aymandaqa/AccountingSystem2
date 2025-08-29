using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using System.Globalization;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "dashboard.view")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardController> _logger;

        public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index(int? branchId = null, string? month = null)
        {
            DateTime selectedMonth;
            if (string.IsNullOrEmpty(month) || !DateTime.TryParseExact(month + "-01", "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out selectedMonth))
            {
                var today = DateTime.Today;
                selectedMonth = new DateTime(today.Year, today.Month, 1);
            }

            var monthEnd = selectedMonth.AddMonths(1).AddTicks(-1);

            var accounts = await _context.Accounts
                .Where(a => a.CanPostTransactions)
                .Where(a => !branchId.HasValue || a.BranchId == branchId || a.BranchId == null)
                .Include(a => a.JournalEntryLines)
                    .ThenInclude(l => l.JournalEntry)
                .ToListAsync();

            var accountBalances = accounts.Select(a => new
            {
                a.AccountType,
                Balance = a.OpeningBalance + a.JournalEntryLines
                    .Where(l => l.JournalEntry.Date <= monthEnd && (!branchId.HasValue || l.JournalEntry.BranchId == branchId))
                    .Sum(l => l.DebitAmount - l.CreditAmount)
            });

            var totals = accountBalances
                .GroupBy(a => a.AccountType)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Balance));

            var viewModel = new DashboardViewModel
            {
                SelectedBranchId = branchId,
                SelectedMonth = selectedMonth,
                TotalAssets = totals.ContainsKey(AccountType.Assets) ? totals[AccountType.Assets] : 0,
                TotalLiabilities = totals.ContainsKey(AccountType.Liabilities) ? totals[AccountType.Liabilities] : 0,
                TotalEquity = totals.ContainsKey(AccountType.Equity) ? totals[AccountType.Equity] : 0,
                TotalRevenues = totals.ContainsKey(AccountType.Revenue) ? totals[AccountType.Revenue] : 0,
                TotalExpenses = totals.ContainsKey(AccountType.Expenses) ? totals[AccountType.Expenses] : 0
            };

            viewModel.NetIncome = viewModel.TotalRevenues - viewModel.TotalExpenses;

            ViewBag.Branches = await _context.Branches
                .Where(b => b.IsActive)
                .Select(b => new { b.Id, b.NameAr })
                .ToListAsync();

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetBranches()
        {
            var branches = await _context.Branches
                .Where(b => b.IsActive)
                .Select(b => new { id = b.Id, nameAr = b.NameAr })
                .ToListAsync();

            return Json(branches);
        }
    }
}

