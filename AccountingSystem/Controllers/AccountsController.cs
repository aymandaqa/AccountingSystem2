using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "accounts.view")]
    public class AccountsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AccountsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Accounts
        public async Task<IActionResult> Index()
        {
            var accounts = await _context.Accounts
                .Include(a => a.Parent)
                .Include(a => a.Branch)
                .OrderBy(a => a.Code)
                .ToListAsync();

            var viewModel = accounts.Select(a => new AccountViewModel
            {
                Id = a.Id,
                Code = a.Code,
                NameAr = a.NameAr,
                NameEn = a.NameEn,
                AccountType = a.AccountType,
                Nature = a.Nature,
                Classification = a.Classification,
                OpeningBalance = a.OpeningBalance,
                CurrentBalance = a.CurrentBalance,
                IsActive = a.IsActive,
                CanPostTransactions = a.CanPostTransactions,
                ParentId = a.ParentId,
                ParentAccountName = a.Parent?.NameAr ?? "",
                BranchId = a.BranchId ?? 0,
                BranchName = a.Branch?.NameAr ?? "",
                Level = a.Level,
                HasChildren = _context.Accounts.Any(x => x.ParentId == a.Id),
                HasTransactions = false
            }).ToList();

            return View(viewModel);
        }

        // GET: Accounts/Tree
        public async Task<IActionResult> Tree()
        {
            var accounts = await _context.Accounts
                .Include(a => a.Children)
                .Where(a => a.ParentId == null)
                .OrderBy(a => a.Code)
                .ToListAsync();

            var treeNodes = accounts.Select(a => MapToTreeNode(a)).ToList();
            return View(treeNodes);
        }

        private AccountTreeNodeViewModel MapToTreeNode(Account account)
        {
            return new AccountTreeNodeViewModel
            {
                Id = account.Id,
                Code = account.Code,
                NameAr = account.NameAr,
                AccountType = account.AccountType,
                Nature = account.Nature,
                OpeningBalance = account.OpeningBalance,
                CurrentBalance = account.CurrentBalance,
                IsActive = account.IsActive,
                CanPostTransactions = account.CanPostTransactions,
                ParentId = account.ParentId,
                Level = account.Level,
                HasChildren = account.Children.Any(),
                Children = account.Children.Select(c => MapToTreeNode(c)).ToList()
            };
        }

        // GET: Accounts/Create
        [Authorize(Policy = "accounts.create")]
        public async Task<IActionResult> Create()
        {
            var viewModel = new CreateAccountViewModel();
            await PopulateDropdowns(viewModel);
            return View(viewModel);
        }

        // POST: Accounts/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "accounts.create")]
        public async Task<IActionResult> Create(CreateAccountViewModel model)
        {
            if (ModelState.IsValid)
            {
                var account = new Account
                {
                    Code = model.Code,
                    NameAr = model.NameAr,
                    NameEn = model.NameEn,
                    AccountType = model.AccountType,
                    Nature = model.Nature,
                    Classification = model.Classification,
                    OpeningBalance = model.OpeningBalance,
                    CurrentBalance = model.OpeningBalance,
                    IsActive = model.IsActive,
                    CanPostTransactions = model.CanPostTransactions,
                    ParentId = model.ParentId,
                    BranchId = model.BranchId,
                    Level = model.ParentId.HasValue ? 
                        (_context.Accounts.Find(model.ParentId.Value)?.Level ?? 0) + 1 : 1
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdowns(model);
            return View(model);
        }

        private async Task PopulateDropdowns(CreateAccountViewModel model)
        {
            model.ParentAccounts = await _context.Accounts
                .Where(a => a.CanPostTransactions == false)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}"
                }).ToListAsync();

            model.Branches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                }).ToListAsync();

            model.CostCenters = await _context.CostCenters
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.NameAr
                }).ToListAsync();
        }
    }
}

