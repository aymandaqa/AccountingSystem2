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

            var treeData = await ComputeDashboardTreeAsync(userBranchIds, branchId, fromDate, toDate, currencyId);

            var accountTypeTrees = treeData.RootNodes
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
                    HasChildren = g.Any(),
                    Children = new List<AccountTreeNodeViewModel>()
                })
                .ToList();

            decimal GetTotal(AccountType type, bool isBase)
            {
                return treeData.TotalsByType.TryGetValue(type, out var totals)
                    ? (isBase ? totals.Base : totals.Selected)
                    : 0m;
            }

            var viewModel = new DashboardViewModel
            {
                SelectedBranchId = branchId,
                SelectedCurrencyId = treeData.SelectedCurrency.Id,
                SelectedCurrencyCode = treeData.SelectedCurrency.Code,
                BaseCurrencyCode = treeData.BaseCurrency.Code,
                FromDate = treeData.StartDate,
                ToDate = treeData.EndDate,
                TotalAssets = GetTotal(AccountType.Assets, false),
                TotalLiabilities = GetTotal(AccountType.Liabilities, false),
                TotalEquity = GetTotal(AccountType.Equity, false),
                TotalRevenues = GetTotal(AccountType.Revenue, false),
                TotalExpenses = GetTotal(AccountType.Expenses, false),
                TotalAssetsBase = GetTotal(AccountType.Assets, true),
                TotalLiabilitiesBase = GetTotal(AccountType.Liabilities, true),
                TotalEquityBase = GetTotal(AccountType.Equity, true),
                TotalRevenuesBase = GetTotal(AccountType.Revenue, true),
                TotalExpensesBase = GetTotal(AccountType.Expenses, true),
                AccountTypeTrees = accountTypeTrees,
                CashBoxTree = treeData.CashBoxTree,
                CashBoxParentAccountConfigured = treeData.CashBoxParentAccountConfigured
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
        public async Task<IActionResult> LoadAccountTreeNodes(AccountType? accountType, int? parentId = null, int? branchId = null, DateTime? fromDate = null, DateTime? toDate = null, int? currencyId = null)
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

            var treeData = await ComputeDashboardTreeAsync(userBranchIds, branchId, fromDate, toDate, currencyId);

            IEnumerable<AccountTreeNodeViewModel> nodesToRender;

            if (parentId.HasValue && parentId.Value > 0)
            {
                if (treeData.NodesById.TryGetValue(parentId.Value, out var parentNode))
                {
                    nodesToRender = parentNode.Children.OrderBy(n => n.Code);
                }
                else
                {
                    nodesToRender = Enumerable.Empty<AccountTreeNodeViewModel>();
                }
            }
            else
            {
                if (!accountType.HasValue)
                {
                    nodesToRender = Enumerable.Empty<AccountTreeNodeViewModel>();
                }
                else
                {
                    nodesToRender = treeData.RootNodes
                        .Where(n => n.AccountType == accountType.Value)
                        .OrderBy(n => n.Code);
                }
            }

            var sanitizedNodes = nodesToRender
                .Select(n => new AccountTreeNodeViewModel
                {
                    Id = n.Id,
                    Code = n.Code,
                    NameAr = n.NameAr,
                    AccountType = n.AccountType,
                    Nature = n.Nature,
                    CurrencyCode = n.CurrencyCode,
                    OpeningBalance = n.OpeningBalance,
                    Balance = n.Balance,
                    BalanceSelected = n.BalanceSelected,
                    BalanceBase = n.BalanceBase,
                    IsActive = n.IsActive,
                    CanPostTransactions = n.CanPostTransactions,
                    ParentId = n.ParentId,
                    Level = n.Level,
                    HasChildren = n.Children.Any(),
                    Children = new List<AccountTreeNodeViewModel>()
                })
                .ToList();

            ViewData["SelectedCurrencyCode"] = treeData.SelectedCurrency.Code;
            ViewData["BaseCurrencyCode"] = treeData.BaseCurrency.Code;

            return PartialView("~/Views/Shared/_AccountBalanceTreeNode.cshtml", sanitizedNodes);
        }

        [HttpGet]
        public async Task<IActionResult> LoadCashBoxTree(int? branchId = null, DateTime? fromDate = null, DateTime? toDate = null, int? currencyId = null)
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

            var treeData = await ComputeDashboardTreeAsync(userBranchIds, branchId, fromDate, toDate, currencyId);

            List<AccountTreeNodeViewModel> CloneNodes(IEnumerable<AccountTreeNodeViewModel> source)
            {
                return source
                    .OrderBy(n => n.Code)
                    .Select(n => new AccountTreeNodeViewModel
                    {
                        Id = n.Id,
                        Code = n.Code,
                        Name = n.Name,
                        NameAr = n.NameAr,
                        AccountType = n.AccountType,
                        Nature = n.Nature,
                        CurrencyCode = n.CurrencyCode,
                        OpeningBalance = n.OpeningBalance,
                        CurrentBalance = n.CurrentBalance,
                        Balance = n.Balance,
                        BalanceSelected = n.BalanceSelected,
                        BalanceBase = n.BalanceBase,
                        IsActive = n.IsActive,
                        CanPostTransactions = n.CanPostTransactions,
                        ParentId = n.ParentId,
                        Level = n.Level,
                        HasChildren = n.Children.Any(),
                        Children = CloneNodes(n.Children)
                    })
                    .ToList();
            }

            var sanitizedNodes = CloneNodes(treeData.CashBoxTree);

            ViewData["SelectedCurrencyCode"] = treeData.SelectedCurrency.Code;
            ViewData["BaseCurrencyCode"] = treeData.BaseCurrency.Code;

            var viewModel = new CashBoxTreeViewModel
            {
                Nodes = sanitizedNodes,
                ParentConfigured = treeData.CashBoxParentAccountConfigured
            };

            return PartialView("~/Views/Dashboard/_CashBoxTreeContent.cshtml", viewModel);
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

        private async Task<DashboardTreeComputationResult> ComputeDashboardTreeAsync(List<int> userBranchIds, int? branchId, DateTime? fromDate, DateTime? toDate, int? currencyId)
        {
            var effectiveBranchId = branchId.HasValue && userBranchIds.Contains(branchId.Value)
                ? branchId
                : null;

            var allowedBranchIds = effectiveBranchId.HasValue ? new List<int> { effectiveBranchId.Value } : userBranchIds;

            var cashBoxParentSetting = await _context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "CashBoxesParentAccountId");

            int? cashBoxParentAccountId = null;
            if (cashBoxParentSetting != null && int.TryParse(cashBoxParentSetting.Value, out var parsedParentId))
            {
                cashBoxParentAccountId = parsedParentId;
            }

            var cashBoxParentConfigured = cashBoxParentAccountId.HasValue;

            var startDate = fromDate?.Date ?? DateTime.Today;
            var toDateValue = toDate?.Date ?? DateTime.Today;
            var endDate = toDateValue.Date.AddDays(1).AddTicks(-1);

            var accounts = await _context.Accounts
                .Where(a => !allowedBranchIds.Any() || a.BranchId == null || allowedBranchIds.Contains(a.BranchId.Value))
                .Include(a => a.Branch)
                .Include(a => a.JournalEntryLines)
                    .ThenInclude(l => l.JournalEntry)
                .Include(a => a.Currency)
                .AsNoTracking()
                .ToListAsync();

            var baseCurrency = await _context.Currencies.FirstAsync(c => c.IsBase);
            var selectedCurrency = currencyId.HasValue
                ? await _context.Currencies.FirstOrDefaultAsync(c => c.Id == currencyId.Value)
                : baseCurrency;
            selectedCurrency ??= baseCurrency;

            var accountBalances = accounts.ToDictionary(a => a.Id, a =>
                a.OpeningBalance + a.JournalEntryLines
                    .Where(l => l.JournalEntry.Date >= startDate && l.JournalEntry.Date <= endDate && allowedBranchIds.Contains(l.JournalEntry.BranchId))
                    .Sum(l => l.DebitAmount - l.CreditAmount));

            var nodes = accounts.Select(a =>
            {
                var balance = accountBalances[a.Id];
                return new AccountTreeNodeViewModel
                {
                    Id = a.Id,
                    Code = a.Code,
                    NameAr = a.NameAr,
                    AccountType = a.AccountType,
                    Nature = a.Nature,
                    CurrencyCode = a.Currency.Code,
                    OpeningBalance = a.OpeningBalance,
                    Balance = balance,
                    BalanceSelected = _currencyService.Convert(balance, a.Currency, selectedCurrency),
                    BalanceBase = _currencyService.Convert(balance, a.Currency, baseCurrency),
                    IsActive = a.IsActive,
                    CanPostTransactions = a.CanPostTransactions,
                    ParentId = a.ParentId,
                    Level = a.Level,
                    Children = new List<AccountTreeNodeViewModel>(),
                    HasChildren = false
                };
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

            var totals = rootNodes
                .GroupBy(n => n.AccountType)
                .ToDictionary(g => g.Key, g => (
                    Selected: g.Sum(n => n.BalanceSelected),
                    Base: g.Sum(n => n.BalanceBase)));

            List<AccountTreeNodeViewModel> cashBoxTreeNodes = new();

            if (cashBoxParentAccountId.HasValue && nodes.TryGetValue(cashBoxParentAccountId.Value, out var cashBoxParentNode))
            {
                AccountTreeNodeViewModel CloneNode(AccountTreeNodeViewModel source, int relativeLevel)
                {
                    var clone = new AccountTreeNodeViewModel
                    {
                        Id = source.Id,
                        Code = source.Code,
                        NameAr = source.NameAr,
                        AccountType = source.AccountType,
                        Nature = source.Nature,
                        CurrencyCode = source.CurrencyCode,
                        OpeningBalance = source.OpeningBalance,
                        Balance = source.Balance,
                        BalanceSelected = source.BalanceSelected,
                        BalanceBase = source.BalanceBase,
                        IsActive = source.IsActive,
                        CanPostTransactions = source.CanPostTransactions,
                        ParentId = relativeLevel == 0 ? null : source.ParentId,
                        Level = relativeLevel,
                        Children = new List<AccountTreeNodeViewModel>(),
                        HasChildren = source.Children.Any()
                    };

                    foreach (var child in source.Children.OrderBy(c => c.Code))
                    {
                        var childClone = CloneNode(child, relativeLevel + 1);
                        clone.Children.Add(childClone);
                    }

                    clone.HasChildren = clone.Children.Any();
                    return clone;
                }

                cashBoxTreeNodes.Add(CloneNode(cashBoxParentNode, 0));
            }

            return new DashboardTreeComputationResult
            {
                RootNodes = rootNodes,
                NodesById = nodes,
                TotalsByType = totals,
                BaseCurrency = baseCurrency,
                SelectedCurrency = selectedCurrency,
                CashBoxTree = cashBoxTreeNodes,
                CashBoxParentAccountConfigured = cashBoxParentConfigured,
                StartDate = startDate,
                EndDate = endDate
            };
        }

        private class DashboardTreeComputationResult
        {
            public List<AccountTreeNodeViewModel> RootNodes { get; set; } = new List<AccountTreeNodeViewModel>();
            public Dictionary<int, AccountTreeNodeViewModel> NodesById { get; set; } = new Dictionary<int, AccountTreeNodeViewModel>();
            public Dictionary<AccountType, (decimal Selected, decimal Base)> TotalsByType { get; set; } = new Dictionary<AccountType, (decimal Selected, decimal Base)>();
            public Currency BaseCurrency { get; set; } = null!;
            public Currency SelectedCurrency { get; set; } = null!;
            public List<AccountTreeNodeViewModel> CashBoxTree { get; set; } = new List<AccountTreeNodeViewModel>();
            public bool CashBoxParentAccountConfigured { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }
    }
}

