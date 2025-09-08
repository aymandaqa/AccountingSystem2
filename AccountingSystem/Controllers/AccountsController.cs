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
        public async Task<IActionResult> Index(string? searchTerm, int page = 1, int pageSize = 10)
        {
            var query = _context.Accounts
                .Include(a => a.Parent)
                .Include(a => a.Branch)
                .Include(a => a.Currency)
                .Where(a => a.IsActive)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(a =>
                    a.Code.Contains(searchTerm) ||
                    a.NameAr.Contains(searchTerm) ||
                    a.NameEn.Contains(searchTerm));
            }

            var totalItems = await query.CountAsync();

            var accounts = await query
                .OrderBy(a => a.Code)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = accounts.Select(a => new AccountViewModel
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
                HasChildren = _context.Accounts.Any(x => x.ParentId == a.Id && x.IsActive),
                HasTransactions = false,
                CurrencyCode = a.Currency.Code
            }).ToList();

            var result = new PagedResult<AccountViewModel>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                SearchTerm = searchTerm
            };

            return View(result);
        }

        // GET: Accounts/Tree
        public async Task<IActionResult> Tree()
        {
            var rootAccounts = await _context.Accounts
                .Include(a => a.Children)
                    .ThenInclude(c => c.Children)
                        .ThenInclude(c => c.Children)
                            .ThenInclude(c => c.Children)
                .Where(a => a.ParentId == null && a.IsActive)
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
                HasChildren = account.Children.Any(c => c.IsActive),
                Children = account.Children
                    .Where(c => c.IsActive)
                    .Select(c => MapToTreeNode(c))
                    .ToList()
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
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                ViewData["IsModal"] = true;
                return PartialView(viewModel);
            }
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
                        (_context.Accounts.Find(model.ParentId.Value)?.Level ?? 0) + 1 : 1,
                    CurrencyId = model.CurrencyId
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                    return Json(new { success = true });
                return RedirectToAction(nameof(Index));
            }

            await PopulateDropdowns(model);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                ViewData["IsModal"] = true;
                return PartialView(model);
            }
            return View(model);
        }

        [HttpPost]
        [Authorize(Policy = "accounts.create")]
        public async Task<IActionResult> GenerateAccountCode(AccountType accountType, int? parentId)
        {
            if (parentId.HasValue)
            {
                var parent = await _context.Accounts
                    .Include(a => a.Children)
                    .FirstOrDefaultAsync(a => a.Id == parentId.Value);
                if (parent == null)
                    return Json(new { success = false, message = "الحساب الأب غير موجود" });

                var lastChildCode = parent.Children
                    .OrderByDescending(c => c.Code)
                    .Select(c => c.Code)
                    .FirstOrDefault();

                var newCode = GenerateChildCode(parent.Code, lastChildCode);
                return Json(new { success = true, code = newCode });
            }

            var baseCode = ((int)accountType).ToString();
            var lastRootCode = await _context.Accounts
                .Where(a => a.ParentId == null && a.AccountType == accountType)
                .OrderByDescending(a => a.Code)
                .Select(a => a.Code)
                .FirstOrDefaultAsync();

            string generatedCode;
            if (string.IsNullOrEmpty(lastRootCode))
            {
                generatedCode = baseCode;
            }
            else if (int.TryParse(lastRootCode, out var rootNumber))
            {
                generatedCode = (rootNumber + 1).ToString();
            }
            else
            {
                generatedCode = baseCode + "1";
            }

            return Json(new { success = true, code = generatedCode });
        }

        private static string GenerateChildCode(string parentCode, string? lastChildCode)
        {
            var segmentLength = parentCode.Length == 1 ? 1 : 2;
            if (string.IsNullOrEmpty(lastChildCode))
                return parentCode + (segmentLength == 1 ? "1" : "01");

            var suffix = lastChildCode.Substring(parentCode.Length);
            if (!int.TryParse(suffix, out var number))
                number = 0;

            return parentCode + (number + 1).ToString(segmentLength == 1 ? "D1" : "D2");
        }

        private async Task PopulateDropdowns(CreateAccountViewModel model)
        {
            model.ParentAccounts = await _context.Accounts
                .Where(a => a.CanPostTransactions == false && a.IsActive)
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

            var currencies = await _context.Currencies.ToListAsync();
            model.Currencies = currencies.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = $"{c.Code} - {c.Name}"
            }).ToList();
            if (model.CurrencyId == 0)
            {
                var baseCurrency = currencies.FirstOrDefault(c => c.IsBase);
                if (baseCurrency != null)
                    model.CurrencyId = baseCurrency.Id;
            }
        }

        private async Task PopulateDropdowns(EditAccountViewModel model)
        {
            model.ParentAccounts = await _context.Accounts
                .Where(a => a.CanPostTransactions == false && a.IsActive)
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

            var currencies = await _context.Currencies.ToListAsync();
            model.Currencies = currencies.Select(c => new SelectListItem
            {
                Value = c.Id.ToString(),
                Text = $"{c.Code} - {c.Name}"
            }).ToList();
            if (model.CurrencyId == 0)
            {
                var baseCurrency = currencies.FirstOrDefault(c => c.IsBase);
                if (baseCurrency != null)
                    model.CurrencyId = baseCurrency.Id;
            }
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
                CurrencyId = account.CurrencyId
            };

            await PopulateDropdowns(model);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                ViewData["IsModal"] = true;
                return PartialView(model);
            }
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
                if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                {
                    ViewData["IsModal"] = true;
                    return PartialView(model);
                }
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
            account.Level = model.ParentId.HasValue ?
                (_context.Accounts.Find(model.ParentId.Value)?.Level ?? 0) + 1 : 1;
            account.CurrencyId = model.CurrencyId;

            await _context.SaveChangesAsync();
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
                return Json(new { success = true });
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "accounts.view")]
        public async Task<IActionResult> Details(int id)
        {
            var account = await _context.Accounts
                .Include(a => a.Parent)
                .Include(a => a.Children)
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (account == null)
                return NotFound();

            var model = new AccountDetailsViewModel
            {
                Id = account.Id,
                Code = account.Code,
                NameAr = account.NameAr,
                NameEn = account.NameEn ?? string.Empty,
                AccountType = account.AccountType,
                Nature = account.Nature,
                SubClassification = account.SubClassification,
                OpeningBalance = account.OpeningBalance,
                CurrentBalance = account.CurrentBalance,
                IsActive = account.IsActive,
                CanPostTransactions = account.CanPostTransactions,
                RequiresCostCenter = false,
                Level = account.Level,
                ParentAccountId = account.ParentId,
                ParentAccountName = account.Parent?.NameAr ?? string.Empty,
                Description = account.Description ?? string.Empty,
                CreatedAt = account.CreatedAt,
                CurrencyCode = account.Currency.Code,
                ChildAccounts = account.Children.Select(c => new AccountDetailsChildViewModel
                {
                    Id = c.Id,
                    Code = c.Code,
                    NameAr = c.NameAr,
                    SubClassification = c.SubClassification,
                    CurrentBalance = c.CurrentBalance,
                    IsActive = c.IsActive
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "accounts.delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == id);
            if (account == null)
                return Json(new { success = false, message = "الحساب غير موجود" });

            var hasChildren = await _context.Accounts.AnyAsync(a => a.ParentId == id && a.IsActive);
            if (hasChildren)
                return Json(new { success = false, message = "لا يمكن حذف الحساب لوجود حسابات فرعية" });

            var hasTransactions = await _context.JournalEntryLines.AnyAsync(l => l.AccountId == id);
            if (hasTransactions)
                return Json(new { success = false, message = "لا يمكن حذف الحساب لوجود حركات مالية" });

            _context.Accounts.Remove(account);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }
    }
}

