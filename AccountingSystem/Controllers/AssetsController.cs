using System;
using System.Collections.Generic;
using System.Linq;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "assets.view")]
    public class AssetsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IJournalEntryService _journalEntryService;
        private readonly IAccountService _accountService;

        public AssetsController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IJournalEntryService journalEntryService,
            IAccountService accountService)
        {
            _context = context;
            _userManager = userManager;
            _journalEntryService = journalEntryService;
            _accountService = accountService;
        }

        public async Task<IActionResult> Index()
        {
            var assets = await _context.Assets
                .Include(a => a.Branch)
                .Include(a => a.AssetType)
                .OrderBy(a => a.Name)
                .ToListAsync();

            var model = assets.Select(a => new AssetListViewModel
            {
                Id = a.Id,
                Name = a.Name,
                AssetTypeName = a.AssetType.Name,
                BranchName = a.Branch.NameAr,
                AssetNumber = a.AssetNumber,
                Notes = a.Notes,
                OpeningBalance = a.OpeningBalance,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            }).ToList();

            return View(model);
        }

        [Authorize(Policy = "assets.create")]
        public async Task<IActionResult> Create()
        {
            var model = new AssetFormViewModel
            {
                Branches = await GetBranchesAsync(),
                CapitalAccounts = await GetCapitalAccountsAsync(),
                AssetTypes = await GetAssetTypesAsync()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assets.create")]
        public async Task<IActionResult> Create(AssetFormViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            Account? parentAccount = null;
            Account? capitalAccount = null;
            AssetType? assetType = null;

            if (ModelState.IsValid)
            {
                assetType = await _context.AssetTypes
                    .Include(t => t.Account)
                    .ThenInclude(a => a.Currency)
                    .FirstOrDefaultAsync(t => t.Id == model.AssetTypeId);

                if (assetType == null)
                {
                    ModelState.AddModelError(nameof(model.AssetTypeId), "نوع الأصل غير صالح");
                }
                else
                {
                    parentAccount = assetType.Account;

                    if (parentAccount == null)
                    {
                        ModelState.AddModelError(nameof(model.AssetTypeId), "نوع الأصل لا يحتوي على حساب مرتبط");
                    }
                }

                capitalAccount = await _context.Accounts
                    .Include(a => a.Currency)
                    .FirstOrDefaultAsync(a => a.Id == model.CapitalAccountId);

                if (capitalAccount == null)
                {
                    ModelState.AddModelError(nameof(model.CapitalAccountId), "حساب رأس المال غير صالح");
                }

                if (parentAccount != null && capitalAccount != null && parentAccount.CurrencyId != capitalAccount.CurrencyId)
                {
                    ModelState.AddModelError(nameof(model.CapitalAccountId), "يجب أن تكون عملة حساب الأصل مطابقة لعملة حساب رأس المال");
                }
            }

            if (!ModelState.IsValid)
            {
                model.Branches = await GetBranchesAsync();
                model.CapitalAccounts = await GetCapitalAccountsAsync();
                model.AssetTypes = await GetAssetTypesAsync();
                return View(model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var (accountId, _) = await _accountService.CreateAccountAsync(model.Name, parentAccount!.Id);
                var account = await _context.Accounts.FindAsync(accountId);
                if (account == null)
                {
                    throw new InvalidOperationException("تعذر إنشاء حساب الأصل");
                }

                account.CanHaveChildren = false;
                account.BranchId = model.BranchId;
                account.Description = model.Notes;
                account.OpeningBalance = 0;
                account.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                var asset = new Asset
                {
                    Name = model.Name,
                    AssetTypeId = model.AssetTypeId,
                    BranchId = model.BranchId,
                    AssetNumber = model.AssetNumber,
                    Notes = model.Notes,
                    OpeningBalance = model.OpeningBalance,
                    AccountId = accountId
                };

                _context.Assets.Add(asset);
                await _context.SaveChangesAsync();

                if (model.OpeningBalance > 0)
                {
                    var lines = new List<JournalEntryLine>
                    {
                        new JournalEntryLine { AccountId = accountId, DebitAmount = model.OpeningBalance },
                        new JournalEntryLine { AccountId = capitalAccount!.Id, CreditAmount = model.OpeningBalance }
                    };

                    var description = $"إثبات أصل جديد: {asset.Name}";
                    if (!string.IsNullOrWhiteSpace(asset.Notes))
                    {
                        description += Environment.NewLine + asset.Notes;
                    }

                    var reference = !string.IsNullOrWhiteSpace(asset.AssetNumber)
                        ? $"ASSET:{asset.AssetNumber}"
                        : $"ASSET:{asset.Id}";

                    await _journalEntryService.CreateJournalEntryAsync(
                        DateTime.Now,
                        description,
                        asset.BranchId,
                        user.Id,
                        lines,
                        JournalEntryStatus.Posted,
                        reference: reference);
                }

                await transaction.CommitAsync();
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "حدث خطأ أثناء إنشاء الأصل. الرجاء المحاولة مرة أخرى");
            }

            model.Branches = await GetBranchesAsync();
            model.CapitalAccounts = await GetCapitalAccountsAsync();
            model.AssetTypes = await GetAssetTypesAsync();
            return View(model);
        }

        [Authorize(Policy = "assets.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var asset = await _context.Assets
                .Include(a => a.Account)
                .Include(a => a.AssetType)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (asset == null)
            {
                return NotFound();
            }

            var model = new AssetFormViewModel
            {
                Id = asset.Id,
                Name = asset.Name,
                AssetTypeId = asset.AssetTypeId,
                BranchId = asset.BranchId,
                AssetNumber = asset.AssetNumber,
                Notes = asset.Notes,
                OpeningBalance = asset.OpeningBalance,
                AccountId = asset.AccountId,
                AccountCode = asset.Account?.Code,
                Branches = await GetBranchesAsync(),
                AssetTypes = await GetAssetTypesAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assets.edit")]
        public async Task<IActionResult> Edit(int id, AssetFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            Asset? asset = null;
            AssetType? assetType = null;

            if (ModelState.IsValid)
            {
                asset = await _context.Assets
                    .Include(a => a.Account)
                    .FirstOrDefaultAsync(a => a.Id == id);
                if (asset == null)
                {
                    return NotFound();
                }

                assetType = await _context.AssetTypes
                    .Include(t => t.Account)
                    .FirstOrDefaultAsync(t => t.Id == model.AssetTypeId);

                if (assetType == null)
                {
                    ModelState.AddModelError(nameof(model.AssetTypeId), "نوع الأصل غير صالح");
                }
                else if (assetType.Account == null)
                {
                    ModelState.AddModelError(nameof(model.AssetTypeId), "نوع الأصل لا يحتوي على حساب مرتبط");
                }
            }

            if (ModelState.IsValid && asset != null && assetType?.Account != null)
            {
                var previousAssetTypeId = asset.AssetTypeId;

                asset.Name = model.Name;
                asset.AssetTypeId = model.AssetTypeId;
                asset.BranchId = model.BranchId;
                asset.AssetNumber = model.AssetNumber;
                asset.Notes = model.Notes;
                asset.OpeningBalance = model.OpeningBalance;
                asset.UpdatedAt = DateTime.Now;

                if (asset.AccountId.HasValue)
                {
                    var account = await _context.Accounts.FindAsync(asset.AccountId.Value);
                    if (account != null)
                    {
                        account.NameAr = model.Name;
                        account.NameEn = model.Name;
                        account.BranchId = model.BranchId;
                        account.Description = model.Notes;
                        account.UpdatedAt = DateTime.Now;

                        if (previousAssetTypeId != model.AssetTypeId)
                        {
                            account.ParentId = assetType.AccountId;
                            account.Level = assetType.Account.Level + 1;
                            account.AccountType = assetType.Account.AccountType;
                            account.Nature = assetType.Account.Nature;
                            account.Classification = assetType.Account.Classification;
                            account.SubClassification = assetType.Account.SubClassification;
                            account.CurrencyId = assetType.Account.CurrencyId;
                        }
                    }
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            model.Branches = await GetBranchesAsync();
            model.AssetTypes = await GetAssetTypesAsync();
            if (model.AccountId.HasValue)
            {
                var account = await _context.Accounts.FindAsync(model.AccountId.Value);
                model.AccountCode = account?.Code;
            }
            return View(model);
        }

        [Authorize(Policy = "assets.delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var asset = await _context.Assets
                .Include(a => a.Branch)
                .Include(a => a.AssetType)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (asset == null)
            {
                return NotFound();
            }

            var model = new AssetListViewModel
            {
                Id = asset.Id,
                Name = asset.Name,
                AssetTypeName = asset.AssetType.Name,
                BranchName = asset.Branch.NameAr,
                AssetNumber = asset.AssetNumber,
                Notes = asset.Notes,
                CreatedAt = asset.CreatedAt,
                UpdatedAt = asset.UpdatedAt
            };

            return View(model);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assets.delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var asset = await _context.Assets.FindAsync(id);
            if (asset == null)
            {
                return NotFound();
            }

            _context.Assets.Remove(asset);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private async Task<IEnumerable<SelectListItem>> GetCapitalAccountsAsync()
        {
            return await _context.Accounts
                .Where(a => a.AccountType == AccountType.Equity && a.CanPostTransactions)
                .OrderBy(a => a.Code)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}"
                }).ToListAsync();
        }

        private async Task<IEnumerable<SelectListItem>> GetBranchesAsync()
        {
            return await _context.Branches
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                }).ToListAsync();
        }

        private async Task<IEnumerable<SelectListItem>> GetAssetTypesAsync()
        {
            return await _context.AssetTypes
                .OrderBy(t => t.Name)
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Account == null || string.IsNullOrWhiteSpace(t.Account.Code)
                        ? t.Name
                        : $"{t.Name} ({t.Account.Code})"
                }).ToListAsync();
        }
    }
}
