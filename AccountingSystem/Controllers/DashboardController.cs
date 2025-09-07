using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;
using AccountingSystem.Services;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "dashboard.view")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<DashboardController> _logger;
        private readonly ICurrencyService _currencyService;

        public DashboardController(ApplicationDbContext context, ILogger<DashboardController> logger, ICurrencyService currencyService)
        {
            _context = context;
            _logger = logger;
            _currencyService = currencyService;
        }

        public async Task<IActionResult> Index(int? branchId = null, DateTime? fromDate = null, DateTime? toDate = null, int? currencyId = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == userId)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            if (branchId.HasValue && !userBranchIds.Contains(branchId.Value))
            {
                branchId = null;
            }

            var allowedBranchIds = branchId.HasValue ? new List<int> { branchId.Value } : userBranchIds;

            var startDate = fromDate?.Date ?? DateTime.Today;
            var endDate = toDate?.Date ?? DateTime.Today;
            endDate = endDate.Date.AddDays(1).AddTicks(-1);

            var accounts = await _context.Accounts
                .Where(a => !allowedBranchIds.Any() || a.BranchId == null || allowedBranchIds.Contains(a.BranchId.Value))
                .Include(a => a.JournalEntryLines)
                    .ThenInclude(l => l.JournalEntry)
                .Include(a => a.Currency)
                .AsNoTracking()
                .ToListAsync();

            var baseCurrency = await _context.Currencies.FirstAsync(c => c.IsBase);
            var selectedCurrency = currencyId.HasValue ? await _context.Currencies.FirstOrDefaultAsync(c => c.Id == currencyId.Value) : baseCurrency;
            selectedCurrency ??= baseCurrency;

            var accountBalances = accounts.ToDictionary(a => a.Id, a =>
                a.OpeningBalance + a.JournalEntryLines
                    .Where(l => l.JournalEntry.Date >= startDate && l.JournalEntry.Date <= endDate && allowedBranchIds.Contains(l.JournalEntry.BranchId))
                    .Sum(l => l.DebitAmount - l.CreditAmount));

            var cashBoxes = accounts
                .Where(a => a.Code.StartsWith("1101"))
                .Select(a => new CashBoxBalanceViewModel
                {
                    AccountName = a.NameAr,
                    BranchName = a.Branch?.NameAr ?? string.Empty,
                    Balance = accountBalances[a.Id],
                    BalanceSelected = _currencyService.Convert(accountBalances[a.Id], a.Currency, selectedCurrency),
                    BalanceBase = _currencyService.Convert(accountBalances[a.Id], a.Currency, baseCurrency)
                })
                .ToList();

            var nodes = accounts.Select(a => new AccountTreeNodeViewModel
            {
                Id = a.Id,
                Code = a.Code,
                NameAr = a.NameAr,
                AccountType = a.AccountType,
                Nature = a.Nature,
                OpeningBalance = a.OpeningBalance,
                Balance = accountBalances[a.Id],
                BalanceSelected = _currencyService.Convert(accountBalances[a.Id], a.Currency, selectedCurrency),
                BalanceBase = _currencyService.Convert(accountBalances[a.Id], a.Currency, baseCurrency),
                IsActive = a.IsActive,
                CanPostTransactions = a.CanPostTransactions,
                ParentId = a.ParentId,
                Level = a.Level,
                Children = new List<AccountTreeNodeViewModel>(),
                HasChildren = false
            }).ToDictionary(n => n.Id);

            foreach (var node in nodes.Values)
            {
                if (node.ParentId.HasValue && nodes.TryGetValue(node.ParentId.Value, out var parent))
                {
                    parent.Children.Add(node);
                    parent.HasChildren = true;
                }
            }

            void ComputeBalances(AccountTreeNodeViewModel node)
            {
                foreach (var child in node.Children)
                {
                    ComputeBalances(child);
                }
                if (node.Children.Any())
                {
                    node.Balance = node.Children.Sum(c => c.Balance);
                    node.BalanceSelected = node.Children.Sum(c => c.BalanceSelected);
                    node.BalanceBase = node.Children.Sum(c => c.BalanceBase);
                }
            }

            var rootNodes = nodes.Values.Where(n => n.ParentId == null).ToList();
            foreach (var root in rootNodes)
            {
                ComputeBalances(root);
            }

            var accountTypeTrees = rootNodes
                .GroupBy(n => n.AccountType)
                .Select(g => new AccountTreeNodeViewModel
                {
                    Id = 0,
                    NameAr = g.Key.ToString(),
                    AccountType = g.Key,
                    Level = 0,
                    Balance = g.Sum(n => n.Balance),
                    BalanceSelected = g.Sum(n => n.BalanceSelected),
                    BalanceBase = g.Sum(n => n.BalanceBase),
                    Children = g.OrderBy(n => n.Code).ToList(),
                    HasChildren = g.Any()
                }).ToList();

            var totals = accountTypeTrees.ToDictionary(n => n.AccountType, n => (n.BalanceSelected, n.BalanceBase));

            var viewModel = new DashboardViewModel
            {
                SelectedBranchId = branchId,
                SelectedCurrencyId = selectedCurrency.Id,
                SelectedCurrencyCode = selectedCurrency.Code,
                BaseCurrencyCode = baseCurrency.Code,
                FromDate = startDate,
                ToDate = endDate,
                TotalAssets = totals.ContainsKey(AccountType.Assets) ? totals[AccountType.Assets].Item1 : 0,
                TotalLiabilities = totals.ContainsKey(AccountType.Liabilities) ? totals[AccountType.Liabilities].Item1 : 0,
                TotalEquity = totals.ContainsKey(AccountType.Equity) ? totals[AccountType.Equity].Item1 : 0,
                TotalRevenues = totals.ContainsKey(AccountType.Revenue) ? totals[AccountType.Revenue].Item1 : 0,
                TotalExpenses = totals.ContainsKey(AccountType.Expenses) ? totals[AccountType.Expenses].Item1 : 0,
                TotalAssetsBase = totals.ContainsKey(AccountType.Assets) ? totals[AccountType.Assets].Item2 : 0,
                TotalLiabilitiesBase = totals.ContainsKey(AccountType.Liabilities) ? totals[AccountType.Liabilities].Item2 : 0,
                TotalEquityBase = totals.ContainsKey(AccountType.Equity) ? totals[AccountType.Equity].Item2 : 0,
                TotalRevenuesBase = totals.ContainsKey(AccountType.Revenue) ? totals[AccountType.Revenue].Item2 : 0,
                TotalExpensesBase = totals.ContainsKey(AccountType.Expenses) ? totals[AccountType.Expenses].Item2 : 0,
                AccountTypeTrees = accountTypeTrees,
                CashBoxes = cashBoxes
            };

            viewModel.NetIncome = viewModel.TotalRevenues - viewModel.TotalExpenses;
            viewModel.NetIncomeBase = viewModel.TotalRevenuesBase - viewModel.TotalExpensesBase;

            ViewBag.Branches = await _context.Branches
                .Where(b => b.IsActive && userBranchIds.Contains(b.Id))
                .Select(b => new { b.Id, b.NameAr })
                .ToListAsync();

            ViewBag.Currencies = await _context.Currencies
                .Select(c => new { c.Id, c.Code })
                .ToListAsync();

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetBranches()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var branches = await _context.UserBranches
                .Where(ub => ub.UserId == userId && ub.Branch.IsActive)
                .Select(ub => new { id = ub.BranchId, nameAr = ub.Branch.NameAr })
                .ToListAsync();

            return Json(branches);
        }
    }
}

