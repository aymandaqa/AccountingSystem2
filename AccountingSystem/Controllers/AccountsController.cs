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
                BranchId = a.BranchId,
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
            var rootAccounts = await _context.Accounts
                .Include(a => a.Children)
                    .ThenInclude(c => c.Children)
                        .ThenInclude(c => c.Children)
                            .ThenInclude(c => c.Children)
                .Where(a => a.ParentId == null)
                .OrderBy(a => a.Code)
                .ToListAsync();

            var grouped = rootAccounts.GroupBy(a => a.AccountType);

            var treeNodes = grouped.Select(g => new AccountTreeNodeViewModel
            {
                Id = 0,
                Code = string.Empty,
                NameAr = g.Key.ToString(),
                AccountType = g.Key,
                Level = 0,
                CanPostTransactions = false,
                IsActive = true,
                HasChildren = g.Any(),
                Children = g.Select(a => MapToTreeNode(a)).ToList()
            }).ToList();

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
        public async Task<IActionResult> Create(int? parentId = null)
        {
            var viewModel = new CreateAccountViewModel
            {
                ParentId = parentId
            };
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
                    BranchId = model.BranchId > 0 ? model.BranchId : null,
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

        private async Task PopulateDropdowns(EditAccountViewModel model)
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

        // GET: Accounts/Edit/5
        [Authorize(Policy = "accounts.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var account = await _context.Accounts.FindAsync(id);
            if (account == null)
            {
                return NotFound();
            }

            var model = new EditAccountViewModel
            {
                Id = account.Id,
                Code = account.Code,
                NameAr = account.NameAr,
                NameEn = account.NameEn,
                AccountType = account.AccountType,
                Nature = account.Nature,
                Classification = account.Classification,
                OpeningBalance = account.OpeningBalance,
                IsActive = account.IsActive,
                CanPostTransactions = account.CanPostTransactions,
                ParentId = account.ParentId,
                BranchId = account.BranchId,
                CostCenterId = account.CostCenterId
            };

            await PopulateDropdowns(model);
            return View(model);
        }

        // POST: Accounts/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "accounts.edit")]
        public async Task<IActionResult> Edit(EditAccountViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(model);
                return View(model);
            }

            var account = await _context.Accounts.FindAsync(model.Id);
            if (account == null)
            {
                return NotFound();
            }

            account.Code = model.Code;
            account.NameAr = model.NameAr;
            account.NameEn = model.NameEn;
            account.AccountType = model.AccountType;
            account.Nature = model.Nature;
            account.Classification = model.Classification;
            account.OpeningBalance = model.OpeningBalance;
            account.IsActive = model.IsActive;
            account.CanPostTransactions = model.CanPostTransactions;
            account.ParentId = model.ParentId;
            account.BranchId = model.BranchId > 0 ? model.BranchId : null;
            account.CostCenterId = model.CostCenterId > 0 ? model.CostCenterId : null;
            account.Level = model.ParentId.HasValue ?
                (_context.Accounts.Find(model.ParentId.Value)?.Level ?? 0) + 1 : 1;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}

