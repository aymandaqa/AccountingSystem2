using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.ViewModels;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assets.create")]
        public async Task<IActionResult> ImportExcel(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ImportErrors"] = "يرجى اختيار ملف Excel صالح.";
                return RedirectToAction(nameof(Index));
            }

            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ImportErrors"] = "يجب أن يكون الملف بامتداد .xlsx";
                return RedirectToAction(nameof(Index));
            }

            var errors = new List<string>();
            var assetsToAdd = new List<Asset>();

            try
            {
                var assetTypes = await _context.AssetTypes
                    .Include(t => t.Account)
                    .ToListAsync();
                var branches = await _context.Branches
                    .AsNoTracking()
                    .ToListAsync();

                var existingAssets = await _context.Assets
                    .Select(a => new { a.BranchId, a.Name })
                    .ToListAsync();
                var existingKeys = new HashSet<string>(existingAssets
                    .Select(a => $"{a.BranchId}|{a.Name}".ToLowerInvariant()));

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.FirstOrDefault();

                if (worksheet == null)
                {
                    errors.Add("تعذر قراءة ورقة العمل من الملف.");
                }
                else
                {
                    var range = worksheet.RangeUsed();
                    if (range == null)
                    {
                        errors.Add("الملف لا يحتوي على بيانات.");
                    }
                    else
                    {
                        foreach (var excelRow in range.RowsUsed().Skip(1))
                        {
                            var usedCells = excelRow.CellsUsed().ToList();
                            if (!usedCells.Any() || usedCells.All(c => string.IsNullOrWhiteSpace(c.GetValue<string>())))
                            {
                                continue;
                            }

                            var rowNumber = excelRow.RowNumber();
                            var name = excelRow.Cell(1).GetValue<string>().Trim();
                            if (string.IsNullOrEmpty(name))
                            {
                                errors.Add($"السطر {rowNumber}: اسم الأصل مطلوب.");
                                continue;
                            }

                            var assetTypeValue = excelRow.Cell(2).GetValue<string>().Trim();
                            if (string.IsNullOrEmpty(assetTypeValue))
                            {
                                errors.Add($"السطر {rowNumber}: نوع الأصل مطلوب.");
                                continue;
                            }

                            var assetType = assetTypes.FirstOrDefault(t =>
                                string.Equals(t.Name, assetTypeValue, StringComparison.OrdinalIgnoreCase) ||
                                (t.Account != null && string.Equals(t.Account.Code, assetTypeValue, StringComparison.OrdinalIgnoreCase)));

                            if (assetType == null)
                            {
                                errors.Add($"السطر {rowNumber}: نوع الأصل \"{assetTypeValue}\" غير معروف.");
                                continue;
                            }

                            if (assetType.AccountId == 0)
                            {
                                errors.Add($"السطر {rowNumber}: نوع الأصل \"{assetType.Name}\" لا يحتوي على حساب مرتبط.");
                                continue;
                            }

                            var branchValue = excelRow.Cell(3).GetValue<string>().Trim();
                            if (string.IsNullOrEmpty(branchValue))
                            {
                                errors.Add($"السطر {rowNumber}: الفرع مطلوب.");
                                continue;
                            }

                            var branch = branches.FirstOrDefault(b =>
                                string.Equals(b.Code, branchValue, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(b.NameAr, branchValue, StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrWhiteSpace(b.NameEn) && string.Equals(b.NameEn, branchValue, StringComparison.OrdinalIgnoreCase)));

                            if (branch == null)
                            {
                                errors.Add($"السطر {rowNumber}: الفرع \"{branchValue}\" غير موجود.");
                                continue;
                            }

                            var key = $"{branch.Id}|{name}".ToLowerInvariant();
                            if (existingKeys.Contains(key))
                            {
                                errors.Add($"السطر {rowNumber}: الأصل \"{name}\" موجود مسبقاً للفرع المحدد.");
                                continue;
                            }

                            decimal openingBalance = 0;
                            var openingCell = excelRow.Cell(5);
                            if (!openingCell.IsEmpty())
                            {
                                if (openingCell.TryGetValue<decimal>(out var openingDecimal))
                                {
                                    openingBalance = openingDecimal;
                                }
                                else
                                {
                                    var openingText = openingCell.GetValue<string>();
                                    if (!string.IsNullOrWhiteSpace(openingText) &&
                                        (decimal.TryParse(openingText, NumberStyles.Any, CultureInfo.InvariantCulture, out openingDecimal) ||
                                         decimal.TryParse(openingText, NumberStyles.Any, CultureInfo.CurrentCulture, out openingDecimal)))
                                    {
                                        openingBalance = openingDecimal;
                                    }
                                    else
                                    {
                                        errors.Add($"السطر {rowNumber}: لا يمكن تحويل قيمة الرصيد الافتتاحي \"{openingText}\".");
                                        continue;
                                    }
                                }
                            }

                            var accountResult = await _accountService.CreateAccountAsync(name, assetType.AccountId);

                            var assetNumber = excelRow.Cell(4).GetValue<string>()?.Trim();
                            var notes = excelRow.Cell(6).GetValue<string>()?.Trim();

                            var asset = new Asset
                            {
                                Name = name,
                                AssetTypeId = assetType.Id,
                                BranchId = branch.Id,
                                AssetNumber = string.IsNullOrWhiteSpace(assetNumber) ? null : assetNumber,
                                Notes = string.IsNullOrWhiteSpace(notes) ? null : notes,
                                OpeningBalance = openingBalance,
                                AccountId = accountResult.Id
                            };

                            assetsToAdd.Add(asset);
                            existingKeys.Add(key);
                        }
                    }
                }

                if (assetsToAdd.Any())
                {
                    _context.Assets.AddRange(assetsToAdd);
                    await _context.SaveChangesAsync();
                    TempData["ImportSuccess"] = $"تم استيراد {assetsToAdd.Count} أصل بنجاح.";
                }

                if (errors.Any())
                {
                    TempData["ImportErrors"] = string.Join(";;", errors);
                }
                else if (!assetsToAdd.Any())
                {
                    TempData["ImportErrors"] = "لم يتم العثور على بيانات لاستيرادها.";
                }
            }
            catch (Exception ex)
            {
                TempData["ImportErrors"] = $"حدث خطأ أثناء قراءة الملف: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
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

        [Authorize(Policy = "assets.view")]
        public async Task<IActionResult> ExportExcel()
        {
            var assets = await _context.Assets
                .AsNoTracking()
                .Include(a => a.Branch)
                .Include(a => a.AssetType)
                .Include(a => a.Account)
                .OrderBy(a => a.Name)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Assets");

            worksheet.Cell(1, 1).Value = "اسم الأصل";
            worksheet.Cell(1, 2).Value = "نوع الأصل";
            worksheet.Cell(1, 3).Value = "الفرع (الكود أو الاسم)";
            worksheet.Cell(1, 4).Value = "رقم الأصل";
            worksheet.Cell(1, 5).Value = "الرصيد الافتتاحي";
            worksheet.Cell(1, 6).Value = "الملاحظات";
            worksheet.Cell(1, 7).Value = "تاريخ الإنشاء";
            worksheet.Cell(1, 8).Value = "كود الحساب";

            worksheet.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var asset in assets)
            {
                worksheet.Cell(row, 1).Value = asset.Name;
                worksheet.Cell(row, 2).Value = asset.AssetType?.Name ?? string.Empty;
                worksheet.Cell(row, 3).Value = string.IsNullOrWhiteSpace(asset.Branch?.Code)
                    ? asset.Branch?.NameAr ?? string.Empty
                    : $"{asset.Branch.Code} - {asset.Branch.NameAr}";
                worksheet.Cell(row, 4).Value = asset.AssetNumber ?? string.Empty;
                worksheet.Cell(row, 5).Value = asset.OpeningBalance;
                worksheet.Cell(row, 6).Value = asset.Notes ?? string.Empty;
                worksheet.Cell(row, 7).Value = asset.CreatedAt;
                worksheet.Cell(row, 7).Style.DateFormat.Format = "yyyy-mm-dd";
                worksheet.Cell(row, 8).Value = asset.Account?.Code ?? string.Empty;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"Assets_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
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
            var asset = await _context.Assets
                .Include(a => a.Account)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (asset == null)
            {
                return NotFound();
            }

            if (asset.AccountId.HasValue)
            {
                var hasTransactions = await _context.JournalEntryLines
                    .AnyAsync(line => line.AccountId == asset.AccountId.Value);

                if (hasTransactions)
                {
                    TempData["ErrorMessage"] = "لا يمكن حذف الأصل لوجود حركات مالية مرتبطة به.";
                    return RedirectToAction(nameof(Index));
                }
            }

            var hasExpenses = await _context.AssetExpenses
                .AnyAsync(expense => expense.AssetId == asset.Id);

            if (hasExpenses)
            {
                TempData["ErrorMessage"] = "لا يمكن حذف الأصل لوجود مصاريف مرتبطة به.";
                return RedirectToAction(nameof(Index));
            }

            if (asset.Account != null)
            {
                _context.Accounts.Remove(asset.Account);
            }

            _context.Assets.Remove(asset);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف الأصل بنجاح.";
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
