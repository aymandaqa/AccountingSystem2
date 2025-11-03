using System;
using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "assettypes.view")]
    public class AssetTypesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAccountService _accountService;

        public AssetTypesController(ApplicationDbContext context, IAccountService accountService)
        {
            _context = context;
            _accountService = accountService;
        }

        public async Task<IActionResult> Index()
        {
            var assetTypes = await _context.AssetTypes
                .Include(t => t.Account)
                .Include(t => t.DepreciationExpenseAccount)
                .Include(t => t.AccumulatedDepreciationAccount)
                .OrderBy(t => t.Name)
                .Select(t => new AssetTypeListViewModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    AccountCode = t.Account != null ? t.Account.Code : string.Empty,
                    AccountName = t.Account != null ? t.Account.NameAr : string.Empty,
                    IsDepreciable = t.IsDepreciable,
                    DepreciationExpenseAccountName = t.DepreciationExpenseAccount != null ? t.DepreciationExpenseAccount.NameAr : null,
                    AccumulatedDepreciationAccountName = t.AccumulatedDepreciationAccount != null ? t.AccumulatedDepreciationAccount.NameAr : null
                }).ToListAsync();

            return View(assetTypes);
        }

        [Authorize(Policy = "assettypes.create")]
        public async Task<IActionResult> Create()
        {
            var model = new AssetTypeFormViewModel
            {
                DepreciationExpenseAccounts = await GetDepreciationExpenseAccountsAsync(),
                AccumulatedDepreciationAccounts = await GetAccumulatedDepreciationAccountsAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assettypes.create")]
        public async Task<IActionResult> Create(AssetTypeFormViewModel model)
        {
            model.Name = model.Name?.Trim() ?? string.Empty;
            Account? depreciationExpenseAccount = null;
            Account? accumulatedDepreciationAccount = null;
            Account? parentAccount = null;

            if (ModelState.IsValid)
            {
                var exists = await _context.AssetTypes
                    .AnyAsync(t => t.Name == model.Name);
                if (exists)
                {
                    ModelState.AddModelError(nameof(model.Name), "اسم نوع الأصل مستخدم من قبل");
                }
            }

            if (ModelState.IsValid)
            {
                parentAccount = await GetAssetTypeParentAccountAsync();
                if (parentAccount == null)
                {
                    ModelState.AddModelError(string.Empty, "لم يتم ضبط حساب أنواع الأصول في الإعدادات");
                }
            }

            if (ModelState.IsValid && model.IsDepreciable)
            {
                if (!model.DepreciationExpenseAccountId.HasValue)
                {
                    ModelState.AddModelError(nameof(model.DepreciationExpenseAccountId), "اختر حساب مصروف الإهلاك");
                }
                else
                {
                    depreciationExpenseAccount = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.Id == model.DepreciationExpenseAccountId.Value && a.CanPostTransactions);

                    if (depreciationExpenseAccount == null || depreciationExpenseAccount.AccountType != AccountType.Expenses)
                    {
                        ModelState.AddModelError(nameof(model.DepreciationExpenseAccountId), "حساب مصروف الإهلاك غير صالح");
                    }
                }

                if (!model.AccumulatedDepreciationAccountId.HasValue)
                {
                    ModelState.AddModelError(nameof(model.AccumulatedDepreciationAccountId), "اختر حساب مجمع الإهلاك");
                }
                else
                {
                    accumulatedDepreciationAccount = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.Id == model.AccumulatedDepreciationAccountId.Value && a.CanPostTransactions);

                    if (accumulatedDepreciationAccount == null || accumulatedDepreciationAccount.AccountType != AccountType.Assets)
                    {
                        ModelState.AddModelError(nameof(model.AccumulatedDepreciationAccountId), "حساب مجمع الإهلاك غير صالح");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                model.DepreciationExpenseAccounts = await GetDepreciationExpenseAccountsAsync();
                model.AccumulatedDepreciationAccounts = await GetAccumulatedDepreciationAccountsAsync();
                return View(model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var (accountId, accountCode) = await _accountService.CreateAccountAsync(model.Name, parentAccount!.Id);
                var account = await _context.Accounts.FindAsync(accountId);
                if (account == null)
                {
                    throw new InvalidOperationException("تعذر إنشاء حساب نوع الأصل");
                }

                account.CanHaveChildren = true;
                account.CanPostTransactions = false;
                account.Description = $"حساب نوع أصل: {model.Name}";
                account.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                var assetType = new AssetType
                {
                    Name = model.Name,
                    AccountId = accountId,
                    IsDepreciable = model.IsDepreciable,
                    DepreciationExpenseAccountId = model.IsDepreciable ? model.DepreciationExpenseAccountId : null,
                    AccumulatedDepreciationAccountId = model.IsDepreciable ? model.AccumulatedDepreciationAccountId : null
                };

                _context.AssetTypes.Add(assetType);
                await _context.SaveChangesAsync();

                await transaction.CommitAsync();
                TempData["Success"] = "تم إنشاء نوع الأصل بنجاح";
                return RedirectToAction(nameof(Index));
            }
            catch
            {
                await transaction.RollbackAsync();
                ModelState.AddModelError(string.Empty, "حدث خطأ أثناء إنشاء نوع الأصل. الرجاء المحاولة مرة أخرى");
            }

            return View(model);
        }

        [Authorize(Policy = "assettypes.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var assetType = await _context.AssetTypes
                .Include(t => t.Account)
                .Include(t => t.DepreciationExpenseAccount)
                .Include(t => t.AccumulatedDepreciationAccount)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (assetType == null)
            {
                return NotFound();
            }

            var model = new AssetTypeFormViewModel
            {
                Id = assetType.Id,
                Name = assetType.Name,
                AccountCode = assetType.Account?.Code,
                IsDepreciable = assetType.IsDepreciable,
                DepreciationExpenseAccountId = assetType.DepreciationExpenseAccountId,
                AccumulatedDepreciationAccountId = assetType.AccumulatedDepreciationAccountId,
                DepreciationExpenseAccounts = await GetDepreciationExpenseAccountsAsync(),
                AccumulatedDepreciationAccounts = await GetAccumulatedDepreciationAccountsAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assettypes.edit")]
        public async Task<IActionResult> Edit(int id, AssetTypeFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            model.Name = model.Name?.Trim() ?? string.Empty;

            Account? depreciationExpenseAccount = null;
            Account? accumulatedDepreciationAccount = null;

            if (ModelState.IsValid)
            {
                var exists = await _context.AssetTypes
                    .AnyAsync(t => t.Id != id && t.Name == model.Name);
                if (exists)
                {
                    ModelState.AddModelError(nameof(model.Name), "اسم نوع الأصل مستخدم من قبل");
                }
            }

            if (ModelState.IsValid && model.IsDepreciable)
            {
                if (!model.DepreciationExpenseAccountId.HasValue)
                {
                    ModelState.AddModelError(nameof(model.DepreciationExpenseAccountId), "اختر حساب مصروف الإهلاك");
                }
                else
                {
                    depreciationExpenseAccount = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.Id == model.DepreciationExpenseAccountId.Value && a.CanPostTransactions);

                    if (depreciationExpenseAccount == null || depreciationExpenseAccount.AccountType != AccountType.Expenses)
                    {
                        ModelState.AddModelError(nameof(model.DepreciationExpenseAccountId), "حساب مصروف الإهلاك غير صالح");
                    }
                }

                if (!model.AccumulatedDepreciationAccountId.HasValue)
                {
                    ModelState.AddModelError(nameof(model.AccumulatedDepreciationAccountId), "اختر حساب مجمع الإهلاك");
                }
                else
                {
                    accumulatedDepreciationAccount = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.Id == model.AccumulatedDepreciationAccountId.Value && a.CanPostTransactions);

                    if (accumulatedDepreciationAccount == null || accumulatedDepreciationAccount.AccountType != AccountType.Assets)
                    {
                        ModelState.AddModelError(nameof(model.AccumulatedDepreciationAccountId), "حساب مجمع الإهلاك غير صالح");
                    }
                }
            }

            if (!ModelState.IsValid)
            {
                model.DepreciationExpenseAccounts = await GetDepreciationExpenseAccountsAsync();
                model.AccumulatedDepreciationAccounts = await GetAccumulatedDepreciationAccountsAsync();
                return View(model);
            }

            var assetType = await _context.AssetTypes
                .Include(t => t.Account)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (assetType == null)
            {
                return NotFound();
            }

            assetType.Name = model.Name;
            if (assetType.Account != null)
            {
                assetType.Account.NameAr = model.Name;
                assetType.Account.NameEn = model.Name;
                assetType.Account.Description = $"حساب نوع أصل: {model.Name}";
                assetType.Account.UpdatedAt = DateTime.Now;
            }

            assetType.IsDepreciable = model.IsDepreciable;
            assetType.DepreciationExpenseAccountId = model.IsDepreciable ? model.DepreciationExpenseAccountId : null;
            assetType.AccumulatedDepreciationAccountId = model.IsDepreciable ? model.AccumulatedDepreciationAccountId : null;

            await _context.SaveChangesAsync();
            TempData["Success"] = "تم تحديث نوع الأصل بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "assettypes.delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var assetType = await _context.AssetTypes
                .Include(t => t.Account)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (assetType == null)
            {
                return NotFound();
            }

            var model = new AssetTypeListViewModel
            {
                Id = assetType.Id,
                Name = assetType.Name,
                AccountCode = assetType.Account?.Code ?? string.Empty,
                AccountName = assetType.Account?.NameAr ?? string.Empty
            };

            return View(model);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assettypes.delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var assetType = await _context.AssetTypes
                .Include(t => t.Assets)
                .Include(t => t.Account)
                    .ThenInclude(a => a.Children)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (assetType == null)
            {
                return NotFound();
            }

            if (assetType.Assets.Any())
            {
                TempData["Error"] = "لا يمكن حذف نوع الأصل لارتباطه بأصول مسجلة";
                return RedirectToAction(nameof(Index));
            }

            if (assetType.Account != null && assetType.Account.Children.Any())
            {
                TempData["Error"] = "لا يمكن حذف نوع الأصل لوجود حسابات فرعية تابعة له";
                return RedirectToAction(nameof(Index));
            }

            if (assetType.Account != null)
            {
                _context.Accounts.Remove(assetType.Account);
            }

            _context.AssetTypes.Remove(assetType);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حذف نوع الأصل بنجاح";
            return RedirectToAction(nameof(Index));
        }

        private async Task<IEnumerable<SelectListItem>> GetDepreciationExpenseAccountsAsync()
        {
            return await _context.Accounts
                .Where(a => a.AccountType == AccountType.Expenses && a.CanPostTransactions)
                .OrderBy(a => a.Code)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}"
                }).ToListAsync();
        }

        private async Task<IEnumerable<SelectListItem>> GetAccumulatedDepreciationAccountsAsync()
        {
            return await _context.Accounts
                .Where(a => a.AccountType == AccountType.Assets && a.CanPostTransactions)
                .OrderBy(a => a.Code)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}"
                }).ToListAsync();
        }

        private async Task<Account?> GetAssetTypeParentAccountAsync()
        {
            var parentSetting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == "AssetTypesParentAccountCode");

            var accountCode = parentSetting?.Value;
            if (string.IsNullOrWhiteSpace(accountCode))
            {
                parentSetting = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.Key == "AssetsParentAccountCode");
                accountCode = parentSetting?.Value;
            }

            if (string.IsNullOrWhiteSpace(accountCode))
            {
                return null;
            }

            return await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Code == accountCode);
        }
    }
}
