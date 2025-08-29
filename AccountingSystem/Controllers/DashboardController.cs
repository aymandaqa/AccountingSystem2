using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using System.Linq;
using System.Collections.Generic;
using System.Security.Claims;

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

        public async Task<IActionResult> Index(int? branchId = null, DateTime? fromDate = null, DateTime? toDate = null)
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
                .AsNoTracking()
                .ToListAsync();

            var accountBalances = accounts.ToDictionary(a => a.Id, a =>
                a.OpeningBalance + a.JournalEntryLines
                    .Where(l => l.JournalEntry.Date >= startDate && l.JournalEntry.Date <= endDate && allowedBranchIds.Contains(l.JournalEntry.BranchId))
                    .Sum(l => l.DebitAmount - l.CreditAmount));

            var nodes = accounts.Select(a => new AccountTreeNodeViewModel
            {
                Id = a.Id,
                Code = a.Code,
                NameAr = a.NameAr,
                AccountType = a.AccountType,
                Nature = a.Nature,
                OpeningBalance = a.OpeningBalance,
                Balance = accountBalances[a.Id],
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

            decimal ComputeBalance(AccountTreeNodeViewModel node)
            {
                if (node.Children.Any())
                {
                    node.Balance = node.Children.Sum(ComputeBalance);
                }
                return node.Balance;
            }

            var rootNodes = nodes.Values.Where(n => n.ParentId == null).ToList();
            foreach (var root in rootNodes)
            {
                ComputeBalance(root);
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
                    Children = g.OrderBy(n => n.Code).ToList(),
                    HasChildren = g.Any()
                }).ToList();

            var totals = accountTypeTrees.ToDictionary(n => n.AccountType, n => n.Balance);

            var viewModel = new DashboardViewModel
            {
                SelectedBranchId = branchId,
                FromDate = startDate,
                ToDate = endDate,
                TotalAssets = totals.ContainsKey(AccountType.Assets) ? totals[AccountType.Assets] : 0,
                TotalLiabilities = totals.ContainsKey(AccountType.Liabilities) ? totals[AccountType.Liabilities] : 0,
                TotalEquity = totals.ContainsKey(AccountType.Equity) ? totals[AccountType.Equity] : 0,
                TotalRevenues = totals.ContainsKey(AccountType.Revenue) ? totals[AccountType.Revenue] : 0,
                TotalExpenses = totals.ContainsKey(AccountType.Expenses) ? totals[AccountType.Expenses] : 0,
                AccountTypeTrees = accountTypeTrees
            };

            viewModel.NetIncome = viewModel.TotalRevenues - viewModel.TotalExpenses;

            ViewBag.Branches = await _context.Branches
                .Where(b => b.IsActive && userBranchIds.Contains(b.Id))
                .Select(b => new { b.Id, b.NameAr })
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

