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
        private readonly IAssetCostCenterService _assetCostCenterService;

        public AssetsController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IJournalEntryService journalEntryService,
            IAccountService accountService,
            IAssetCostCenterService assetCostCenterService)
        {
            _context = context;
            _userManager = userManager;
            _journalEntryService = journalEntryService;
            _accountService = accountService;
            _assetCostCenterService = assetCostCenterService;
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

                    await _assetCostCenterService.EnsureCostCentersAsync(assetsToAdd);
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
            ViewBag.AssetSaleSuccess = TempData["AssetSaleSuccess"] as string;
            ViewBag.AssetSaleError = TempData["AssetSaleError"] as string;

            var assets = await _context.Assets
                .Where(a => !a.IsDisposed)
                .Include(a => a.Branch)
                .Include(a => a.AssetType)
                .Include(a => a.Account)
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
                AccountId = a.AccountId,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt,
                IsDepreciable = a.AssetType.IsDepreciable && !a.IsDisposed,
                AccumulatedDepreciation = a.AccumulatedDepreciation,
                BookValue = a.BookValue,
                IsDisposed = a.IsDisposed,
                DisposedAt = a.DisposedAt,
                DisposalProceeds = a.DisposalProceeds,
                DisposalProfitLoss = a.DisposalProfitLoss,
                BookValueAtDisposal = a.BookValueAtDisposal
            }).ToList();

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assets.edit")]
        public async Task<IActionResult> Sell(int id, decimal salePrice)
        {
            if (salePrice < 0)
            {
                TempData["AssetSaleError"] = "قيمة البيع يجب أن تكون مساوية أو أكبر من صفر.";
                return RedirectToAction(nameof(Index));
            }

            var asset = await _context.Assets
                .Include(a => a.AssetType)
                    .ThenInclude(t => t.AccumulatedDepreciationAccount)
                .Include(a => a.Branch)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (asset == null)
            {
                TempData["AssetSaleError"] = "الأصل غير موجود.";
                return RedirectToAction(nameof(Index));
            }

            if (asset.IsDisposed)
            {
                TempData["AssetSaleError"] = "تم بيع الأصل بالفعل.";
                return RedirectToAction(nameof(Index));
            }

            if (asset.AssetType == null)
            {
                TempData["AssetSaleError"] = "نوع الأصل غير محدد.";
                return RedirectToAction(nameof(Index));
            }

            if (!asset.AccountId.HasValue)
            {
                TempData["AssetSaleError"] = "الأصل غير مرتبط بحساب محاسبي.";
                return RedirectToAction(nameof(Index));
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var profitLoss = salePrice - asset.BookValue;
            var accumulatedDepreciation = asset.AccumulatedDepreciation;
            var assetCost = accumulatedDepreciation + asset.BookValue;
            var reference = $"ASSETSALE:{asset.Id}";

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                var settingsKeys = new[]
                {
                    "AssetSaleProceedsAccountId",
                    "AssetSaleGainAccountId",
                    "AssetSaleLossAccountId"
                };

                var settings = await _context.SystemSettings
                    .Where(s => settingsKeys.Contains(s.Key))
                    .ToDictionaryAsync(s => s.Key, s => s.Value);

                int GetAccountId(string key, string friendlyName, bool required)
                {
                    if (!settings.TryGetValue(key, out var value) || string.IsNullOrWhiteSpace(value))
                    {
                        if (!required)
                        {
                            return 0;
                        }

                        throw new InvalidOperationException($"يرجى تحديد {friendlyName} في إعدادات النظام.");
                    }

                    if (!int.TryParse(value, out var accountId))
                    {
                        throw new InvalidOperationException($"قيمة إعداد {friendlyName} غير صالحة.");
                    }

                    return accountId;
                }

                var proceedsAccountId = salePrice > 0
                    ? GetAccountId("AssetSaleProceedsAccountId", "حساب متحصلات بيع الأصول", required: true)
                    : 0;

                var gainAccountId = profitLoss > 0
                    ? GetAccountId("AssetSaleGainAccountId", "حساب أرباح بيع الأصول", required: true)
                    : 0;

                var lossAccountId = profitLoss < 0
                    ? GetAccountId("AssetSaleLossAccountId", "حساب خسائر بيع الأصول", required: true)
                    : 0;

                if (accumulatedDepreciation > 0 && (!asset.AssetType.IsDepreciable || !asset.AssetType.AccumulatedDepreciationAccountId.HasValue))
                {
                    throw new InvalidOperationException("نوع الأصل لا يحتوي على حساب مجمع إهلاك صالح.");
                }

                var lines = new List<JournalEntryLine>();

                if (salePrice > 0)
                {
                    lines.Add(new JournalEntryLine
                    {
                        AccountId = proceedsAccountId,
                        DebitAmount = salePrice,
                        Description = "بيع أصل"
                    });
                }

                if (accumulatedDepreciation > 0 && asset.AssetType.AccumulatedDepreciationAccountId.HasValue)
                {
                    lines.Add(new JournalEntryLine
                    {
                        AccountId = asset.AssetType.AccumulatedDepreciationAccountId.Value,
                        DebitAmount = accumulatedDepreciation,
                        Description = "إلغاء مجمع الإهلاك"
                    });
                }

                if (assetCost > 0)
                {
                    lines.Add(new JournalEntryLine
                    {
                        AccountId = asset.AccountId.Value,
                        CreditAmount = assetCost,
                        Description = "إلغاء قيمة الأصل"
                    });
                }

                if (profitLoss > 0)
                {
                    lines.Add(new JournalEntryLine
                    {
                        AccountId = gainAccountId,
                        CreditAmount = profitLoss,
                        Description = "ربح بيع أصل"
                    });
                }
                else if (profitLoss < 0)
                {
                    lines.Add(new JournalEntryLine
                    {
                        AccountId = lossAccountId,
                        DebitAmount = Math.Abs(profitLoss),
                        Description = "خسارة بيع أصل"
                    });
                }

                if (!lines.Any())
                {
                    throw new InvalidOperationException("لا يمكن إنشاء قيد بدون حركات.");
                }

                var descriptionLines = new List<string>
                {
                    $"بيع الأصل: {asset.Name}"
                };

                if (!string.IsNullOrWhiteSpace(asset.AssetNumber))
                {
                    descriptionLines.Add($"رقم الأصل: {asset.AssetNumber}");
                }

                var description = string.Join(Environment.NewLine, descriptionLines);

                var existingEntry = await _context.JournalEntries
                    .AsNoTracking()
                    .FirstOrDefaultAsync(e => e.Reference == reference);

                if (existingEntry != null)
                {
                    throw new InvalidOperationException("تم إنشاء قيد بيع لهذا الأصل مسبقًا.");
                }

                await _journalEntryService.CreateJournalEntryAsync(
                    DateTime.Now,
                    description,
                    asset.BranchId,
                    user.Id,
                    lines,
                    JournalEntryStatus.Posted,
                    reference: reference);

                var bookValueAtSale = asset.BookValue;

                asset.IsDisposed = true;
                asset.DisposedAt = DateTime.Now;
                asset.DisposalProceeds = salePrice;
                asset.BookValueAtDisposal = bookValueAtSale;
                asset.DisposalProfitLoss = profitLoss;
                asset.BookValue = 0m;
                asset.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                string resultMessage;
                if (profitLoss > 0)
                {
                    resultMessage = $"تحقق ربح قدره {profitLoss:N2}.";
                }
                else if (profitLoss < 0)
                {
                    resultMessage = $"تحقق خسارة قدرها {Math.Abs(profitLoss):N2}.";
                }
                else
                {
                    resultMessage = "لم يتحقق ربح أو خسارة.";
                }

                TempData["AssetSaleSuccess"] = $"تم بيع الأصل \"{asset.Name}\" بنجاح. {resultMessage}";
            }
            catch (InvalidOperationException ex)
            {
                await transaction.RollbackAsync();
                TempData["AssetSaleError"] = ex.Message;
            }
            catch (Exception)
            {
                await transaction.RollbackAsync();
                TempData["AssetSaleError"] = "حدث خطأ أثناء إنشاء القيد المحاسبي لبيع الأصل.";
            }

            return RedirectToAction(nameof(Index));
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
            worksheet.Cell(1, 9).Value = "مجمع الإهلاك";
            worksheet.Cell(1, 10).Value = "القيمة الدفترية";

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
                worksheet.Cell(row, 9).Value = asset.AccumulatedDepreciation;
                worksheet.Cell(row, 10).Value = asset.BookValue;
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
            var assetTypeSelection = await GetAssetTypeSelectionDataAsync();
            var model = new AssetFormViewModel
            {
                Branches = await GetBranchesAsync(),
                CapitalAccounts = await GetCapitalAccountsAsync(),
                AssetTypes = assetTypeSelection.SelectList,
                AssetTypeOptions = assetTypeSelection.Options,
                Suppliers = await GetSuppliersAsync(),
                DepreciationFrequencies = GetDepreciationFrequencySelectList(),
                PurchaseDate = DateTime.Today
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
            Account? supplierAccount = null;
            AssetType? assetType = null;

            if (ModelState.IsValid)
            {
                assetType = await _context.AssetTypes
                    .Include(t => t.Account)
                        .ThenInclude(a => a.Currency)
                    .Include(t => t.DepreciationExpenseAccount)
                    .Include(t => t.AccumulatedDepreciationAccount)
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

                if (model.SupplierId.HasValue && model.CapitalAccountId.HasValue)
                {
                    ModelState.AddModelError(string.Empty, "يرجى اختيار حساب واحد للقيد: المورد أو رأس المال");
                }
            }

            if (ModelState.IsValid && model.SupplierId.HasValue && (!model.PurchaseAmount.HasValue || model.PurchaseAmount.Value <= 0))
            {
                ModelState.AddModelError(nameof(model.PurchaseAmount), "قيمة الشراء مطلوبة عند اختيار مورد");
            }

            if (ModelState.IsValid && model.PurchaseAmount.HasValue && model.PurchaseAmount.Value > 0 && !model.SupplierId.HasValue)
            {
                ModelState.AddModelError(nameof(model.SupplierId), "حدد المورد المرتبط بقيمة الشراء");
            }

            if (ModelState.IsValid && model.CapitalAccountId.HasValue)
            {
                capitalAccount = await _context.Accounts
                    .Include(a => a.Currency)
                    .FirstOrDefaultAsync(a => a.Id == model.CapitalAccountId.Value);

                if (capitalAccount == null)
                {
                    ModelState.AddModelError(nameof(model.CapitalAccountId), "حساب رأس المال غير صالح");
                }
            }

            if (ModelState.IsValid && model.SupplierId.HasValue)
            {
                var supplier = await _context.Suppliers
                    .Include(s => s.Account)
                        .ThenInclude(a => a.Currency)
                    .FirstOrDefaultAsync(s => s.Id == model.SupplierId.Value);

                if (supplier == null || supplier.Account == null)
                {
                    ModelState.AddModelError(nameof(model.SupplierId), "المورد المحدد غير صالح أو لا يحتوي على حساب مرتبط");
                }
                else
                {
                    supplierAccount = supplier.Account;
                }
            }

            if (ModelState.IsValid && parentAccount != null && !model.SupplierId.HasValue && !model.CapitalAccountId.HasValue)
            {
                ModelState.AddModelError(nameof(model.CapitalAccountId), "يرجى اختيار حساب المورد أو حساب رأس المال");
            }

            if (ModelState.IsValid && parentAccount != null && capitalAccount != null && parentAccount.CurrencyId != capitalAccount.CurrencyId)
            {
                ModelState.AddModelError(nameof(model.CapitalAccountId), "يجب أن تكون عملة حساب الأصل مطابقة لعملة حساب رأس المال");
            }

            if (ModelState.IsValid && parentAccount != null && supplierAccount != null && parentAccount.CurrencyId != supplierAccount.CurrencyId)
            {
                ModelState.AddModelError(nameof(model.SupplierId), "يجب أن تكون عملة حساب الأصل مطابقة لعملة حساب المورد");
            }

            if (ModelState.IsValid && assetType != null && assetType.IsDepreciable)
            {
                if (!model.OriginalCost.HasValue || model.OriginalCost.Value <= 0)
                {
                    ModelState.AddModelError(nameof(model.OriginalCost), "قيمة الأصل مطلوبة للأصول القابلة للإهلاك");
                }

                if (assetType.DepreciationExpenseAccountId == null)
                {
                    ModelState.AddModelError(nameof(model.AssetTypeId), "نوع الأصل لا يحتوي على حساب مصروف الإهلاك");
                }

                if (assetType.AccumulatedDepreciationAccountId == null)
                {
                    ModelState.AddModelError(nameof(model.AssetTypeId), "نوع الأصل لا يحتوي على حساب مجمع الإهلاك");
                }

                var salvageValue = model.SalvageValue ?? 0m;
                if (salvageValue < 0)
                {
                    ModelState.AddModelError(nameof(model.SalvageValue), "قيمة الخردة يجب أن تكون موجبة");
                }

                if (model.OriginalCost.HasValue && salvageValue > model.OriginalCost.Value)
                {
                    ModelState.AddModelError(nameof(model.SalvageValue), "قيمة الخردة يجب ألا تتجاوز قيمة الأصل");
                }

                if (!model.DepreciationPeriods.HasValue || model.DepreciationPeriods.Value <= 0)
                {
                    ModelState.AddModelError(nameof(model.DepreciationPeriods), "العمر الافتراضي مطلوب");
                }

                if (!model.DepreciationFrequency.HasValue)
                {
                    ModelState.AddModelError(nameof(model.DepreciationFrequency), "حدد دورية الإهلاك");
                }

                if (!model.PurchaseDate.HasValue)
                {
                    ModelState.AddModelError(nameof(model.PurchaseDate), "تاريخ الشراء مطلوب");
                }
            }

            if (!ModelState.IsValid)
            {
                var assetTypeSelection = await GetAssetTypeSelectionDataAsync(model.AssetTypeId);
                model.Branches = await GetBranchesAsync();
                model.CapitalAccounts = await GetCapitalAccountsAsync();
                model.AssetTypes = assetTypeSelection.SelectList;
                model.AssetTypeOptions = assetTypeSelection.Options;
                model.DepreciationFrequencies = GetDepreciationFrequencySelectList(model.DepreciationFrequency);
                model.Suppliers = await GetSuppliersAsync();
                if (assetType != null && assetType.IsDepreciable && model.OriginalCost.HasValue)
                {
                    model.OpeningBalance = model.OriginalCost.Value;
                }
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

                var openingBalanceValue = model.OpeningBalance;
                var bookValue = model.OpeningBalance;
                var journalAmount = model.PurchaseAmount ?? model.OpeningBalance;
                decimal? originalCost = null;
                decimal? salvageValue = null;
                int? depreciationPeriods = null;
                DepreciationFrequency? depreciationFrequency = null;
                DateTime? purchaseDate = null;

                if (assetType!.IsDepreciable)
                {
                    originalCost = model.OriginalCost ?? 0m;
                    salvageValue = model.SalvageValue ?? 0m;
                    depreciationPeriods = model.DepreciationPeriods;
                    depreciationFrequency = model.DepreciationFrequency;
                    purchaseDate = model.PurchaseDate;
                    openingBalanceValue = originalCost ?? 0m;
                    bookValue = originalCost ?? 0m;
                    journalAmount = model.PurchaseAmount ?? (originalCost ?? 0m);
                }
                else if (model.PurchaseAmount.HasValue)
                {
                    openingBalanceValue = model.PurchaseAmount.Value;
                    bookValue = model.PurchaseAmount.Value;
                }

                var asset = new Asset
                {
                    Name = model.Name,
                    AssetTypeId = model.AssetTypeId,
                    BranchId = model.BranchId,
                    AssetNumber = model.AssetNumber,
                    Notes = model.Notes,
                    OpeningBalance = openingBalanceValue,
                    AccountId = accountId,
                    OriginalCost = originalCost,
                    SalvageValue = salvageValue,
                    DepreciationPeriods = depreciationPeriods,
                    DepreciationFrequency = depreciationFrequency,
                    PurchaseDate = purchaseDate,
                    SupplierId = model.SupplierId,
                    PurchaseAmount = model.PurchaseAmount,
                    AccumulatedDepreciation = 0,
                    BookValue = bookValue
                };

                _context.Assets.Add(asset);
                await _context.SaveChangesAsync();

                await _assetCostCenterService.EnsureCostCenterAsync(asset);
                await _context.SaveChangesAsync();

                if (journalAmount > 0 && (capitalAccount != null || supplierAccount != null))
                {
                    var lines = new List<JournalEntryLine>
                    {
                        new JournalEntryLine { AccountId = accountId, DebitAmount = journalAmount }
                    };

                    var creditAccountId = supplierAccount?.Id ?? capitalAccount?.Id ?? 0;
                    if (creditAccountId == 0)
                    {
                        throw new InvalidOperationException("تعذر تحديد الحساب الدائن للقيد المحاسبي للأصل");
                    }
                    lines.Add(new JournalEntryLine { AccountId = creditAccountId, CreditAmount = journalAmount });

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

            var selection = await GetAssetTypeSelectionDataAsync(model.AssetTypeId);
            model.Branches = await GetBranchesAsync();
            model.CapitalAccounts = await GetCapitalAccountsAsync();
            model.AssetTypes = selection.SelectList;
            model.AssetTypeOptions = selection.Options;
            model.DepreciationFrequencies = GetDepreciationFrequencySelectList(model.DepreciationFrequency);
            model.Suppliers = await GetSuppliersAsync();
            if (assetType != null && assetType.IsDepreciable && model.OriginalCost.HasValue)
            {
                model.OpeningBalance = model.OriginalCost.Value;
            }
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

            var assetTypeSelection = await GetAssetTypeSelectionDataAsync(asset.AssetTypeId);
            var suppliers = await GetSuppliersAsync();
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
                OriginalCost = asset.OriginalCost,
                SalvageValue = asset.SalvageValue,
                DepreciationPeriods = asset.DepreciationPeriods,
                DepreciationFrequency = asset.DepreciationFrequency,
                PurchaseDate = asset.PurchaseDate,
                SupplierId = asset.SupplierId,
                PurchaseAmount = asset.PurchaseAmount,
                Branches = await GetBranchesAsync(),
                AssetTypes = assetTypeSelection.SelectList,
                AssetTypeOptions = assetTypeSelection.Options,
                DepreciationFrequencies = GetDepreciationFrequencySelectList(asset.DepreciationFrequency),
                Suppliers = suppliers
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
            Account? supplierAccount = null;

            if (ModelState.IsValid)
            {
                asset = await _context.Assets
                    .Include(a => a.Account)
                    .Include(a => a.Depreciations)
                    .FirstOrDefaultAsync(a => a.Id == id);
                if (asset == null)
                {
                    return NotFound();
                }

                assetType = await _context.AssetTypes
                    .Include(t => t.Account)
                    .Include(t => t.DepreciationExpenseAccount)
                    .Include(t => t.AccumulatedDepreciationAccount)
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

            if (ModelState.IsValid && model.SupplierId.HasValue && (!model.PurchaseAmount.HasValue || model.PurchaseAmount.Value <= 0))
            {
                ModelState.AddModelError(nameof(model.PurchaseAmount), "قيمة الشراء مطلوبة عند اختيار مورد");
            }

            if (ModelState.IsValid && model.PurchaseAmount.HasValue && model.PurchaseAmount.Value > 0 && !model.SupplierId.HasValue)
            {
                ModelState.AddModelError(nameof(model.SupplierId), "حدد المورد المرتبط بقيمة الشراء");
            }

            if (ModelState.IsValid && model.SupplierId.HasValue)
            {
                var supplier = await _context.Suppliers
                    .Include(s => s.Account)
                        .ThenInclude(a => a.Currency)
                    .FirstOrDefaultAsync(s => s.Id == model.SupplierId.Value);

                if (supplier == null || supplier.Account == null)
                {
                    ModelState.AddModelError(nameof(model.SupplierId), "المورد المحدد غير صالح أو لا يحتوي على حساب مرتبط");
                }
                else
                {
                    supplierAccount = supplier.Account;
                }
            }

            if (ModelState.IsValid && assetType?.Account != null && supplierAccount != null && assetType.Account.CurrencyId != supplierAccount.CurrencyId)
            {
                ModelState.AddModelError(nameof(model.SupplierId), "يجب أن تكون عملة حساب الأصل مطابقة لعملة حساب المورد");
            }

            if (ModelState.IsValid && asset != null && assetType != null && assetType.IsDepreciable)
            {
                if (!model.OriginalCost.HasValue || model.OriginalCost.Value <= 0)
                {
                    ModelState.AddModelError(nameof(model.OriginalCost), "قيمة الأصل مطلوبة للأصول القابلة للإهلاك");
                }

                if (assetType.DepreciationExpenseAccountId == null)
                {
                    ModelState.AddModelError(nameof(model.AssetTypeId), "نوع الأصل لا يحتوي على حساب مصروف الإهلاك");
                }

                if (assetType.AccumulatedDepreciationAccountId == null)
                {
                    ModelState.AddModelError(nameof(model.AssetTypeId), "نوع الأصل لا يحتوي على حساب مجمع الإهلاك");
                }

                var salvageValue = model.SalvageValue ?? 0m;
                if (salvageValue < 0)
                {
                    ModelState.AddModelError(nameof(model.SalvageValue), "قيمة الخردة يجب أن تكون موجبة");
                }

                if (model.OriginalCost.HasValue && salvageValue > model.OriginalCost.Value)
                {
                    ModelState.AddModelError(nameof(model.SalvageValue), "قيمة الخردة يجب ألا تتجاوز قيمة الأصل");
                }

                if (!model.DepreciationPeriods.HasValue || model.DepreciationPeriods.Value <= 0)
                {
                    ModelState.AddModelError(nameof(model.DepreciationPeriods), "العمر الافتراضي مطلوب");
                }

                if (!model.DepreciationFrequency.HasValue)
                {
                    ModelState.AddModelError(nameof(model.DepreciationFrequency), "حدد دورية الإهلاك");
                }

                if (!model.PurchaseDate.HasValue)
                {
                    ModelState.AddModelError(nameof(model.PurchaseDate), "تاريخ الشراء مطلوب");
                }

                if (model.OriginalCost.HasValue)
                {
                    var maxAccumulated = model.OriginalCost.Value - (model.SalvageValue ?? 0m);
                    if (asset.AccumulatedDepreciation > maxAccumulated)
                    {
                        ModelState.AddModelError(nameof(model.OriginalCost), "قيمة الأصل يجب أن تغطي مجمع الإهلاك الحالي");
                    }
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
                asset.UpdatedAt = DateTime.Now;
                asset.SupplierId = model.SupplierId;
                asset.PurchaseAmount = model.PurchaseAmount;

                if (assetType.IsDepreciable && model.OriginalCost.HasValue)
                {
                    asset.OpeningBalance = model.OriginalCost.Value;
                }
                else
                {
                    asset.OpeningBalance = model.PurchaseAmount ?? model.OpeningBalance;
                }

                if (assetType.IsDepreciable)
                {
                    asset.OriginalCost = model.OriginalCost ?? asset.OriginalCost;
                    asset.SalvageValue = model.SalvageValue ?? 0m;
                    asset.DepreciationPeriods = model.DepreciationPeriods;
                    asset.DepreciationFrequency = model.DepreciationFrequency;
                    asset.PurchaseDate = model.PurchaseDate;

                    var currentBookValue = (asset.OriginalCost ?? 0m) - asset.AccumulatedDepreciation;
                    if (asset.SalvageValue.HasValue && currentBookValue < asset.SalvageValue.Value)
                    {
                        currentBookValue = asset.SalvageValue.Value;
                    }

                    asset.BookValue = currentBookValue;
                }
                else
                {
                    asset.OriginalCost = null;
                    asset.SalvageValue = null;
                    asset.DepreciationPeriods = null;
                    asset.DepreciationFrequency = null;
                    asset.PurchaseDate = null;
                    asset.AccumulatedDepreciation = 0;
                    asset.BookValue = asset.OpeningBalance;
                }

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

                await _assetCostCenterService.EnsureCostCenterAsync(asset);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            var assetTypeSelection = await GetAssetTypeSelectionDataAsync(model.AssetTypeId);
            model.Branches = await GetBranchesAsync();
            model.AssetTypes = assetTypeSelection.SelectList;
            model.AssetTypeOptions = assetTypeSelection.Options;
            model.DepreciationFrequencies = GetDepreciationFrequencySelectList(model.DepreciationFrequency);
            model.Suppliers = await GetSuppliersAsync();
            if (assetType != null && assetType.IsDepreciable && model.OriginalCost.HasValue)
            {
                model.OpeningBalance = model.OriginalCost.Value;
            }
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
                    .ThenInclude(a => a!.JournalEntryLines)
                .Include(a => a.Expenses)
                .Include(a => a.Depreciations)
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

            if (asset.Expenses.Any())
            {
                TempData["ErrorMessage"] = "لا يمكن حذف الأصل لوجود مصاريف مرتبطة به.";
                return RedirectToAction(nameof(Index));
            }

            if (asset.Depreciations.Any())
            {
                TempData["ErrorMessage"] = "لا يمكن حذف الأصل لوجود قيود إهلاك مرتبطة به.";
                return RedirectToAction(nameof(Index));
            }

            await _assetCostCenterService.RemoveCostCenterAsync(asset);

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

        private async Task<IEnumerable<SelectListItem>> GetSuppliersAsync()
        {
            return await _context.Suppliers
                .Include(s => s.Account)
                .Where(s => s.AccountId != null && s.Account != null)
                .OrderBy(s => s.NameAr)
                .Select(s => new SelectListItem
                {
                    Value = s.Id.ToString(),
                    Text = s.NameAr
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

        private async Task<AssetTypeSelectionData> GetAssetTypeSelectionDataAsync(int? selectedId = null)
        {
            var assetTypes = await _context.AssetTypes
                .Include(t => t.Account)
                .OrderBy(t => t.Name)
                .Select(t => new
                {
                    t.Id,
                    t.Name,
                    t.IsDepreciable,
                    AccountCode = t.Account != null ? t.Account.Code : null
                })
                .ToListAsync();

            var selectList = assetTypes.Select(t => new SelectListItem
            {
                Value = t.Id.ToString(),
                Text = string.IsNullOrWhiteSpace(t.AccountCode)
                    ? t.Name
                    : $"{t.Name} ({t.AccountCode})",
                Selected = selectedId.HasValue && selectedId.Value == t.Id
            }).ToList();

            var options = assetTypes.Select(t => new AssetTypeSelectOption
            {
                Id = t.Id,
                IsDepreciable = t.IsDepreciable
            }).ToList();

            return new AssetTypeSelectionData
            {
                SelectList = selectList,
                Options = options
            };
        }

        private IEnumerable<SelectListItem> GetDepreciationFrequencySelectList(DepreciationFrequency? selected = null)
        {
            return new List<SelectListItem>
            {
                new SelectListItem
                {
                    Value = ((int)DepreciationFrequency.Monthly).ToString(),
                    Text = "شهري",
                    Selected = selected == DepreciationFrequency.Monthly
                },
                new SelectListItem
                {
                    Value = ((int)DepreciationFrequency.Yearly).ToString(),
                    Text = "سنوي",
                    Selected = selected == DepreciationFrequency.Yearly
                }
            };
        }

        private sealed class AssetTypeSelectionData
        {
            public List<SelectListItem> SelectList { get; set; } = new();
            public List<AssetTypeSelectOption> Options { get; set; } = new();
        }
    }
}
