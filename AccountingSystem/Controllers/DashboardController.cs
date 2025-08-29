using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;

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

        public async Task<IActionResult> Index(int? branchId = null)
        {
            var accounts = await _context.Accounts
                .Where(a => a.CanPostTransactions)
                .Where(a => !branchId.HasValue || a.BranchId == branchId || a.BranchId == null)
                .ToListAsync();

            var viewModel = new DashboardViewModel
            {
                SelectedBranchId = branchId,
                TotalAssets = accounts
                    .Where(a => a.AccountType == AccountType.Assets)
                    .Sum(a => a.CurrentBalance),
                TotalRevenues = accounts
                    .Where(a => a.AccountType == AccountType.Revenue)
                    .Sum(a => a.CurrentBalance),
                TotalExpenses = accounts
                    .Where(a => a.AccountType == AccountType.Expenses)
                    .Sum(a => a.CurrentBalance)
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

