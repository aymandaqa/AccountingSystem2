using System;
using System.Collections;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Text.Json;
using AccountingSystem.Data;
using AccountingSystem.Extensions;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using Syncfusion.EJ2.Base;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "suppliers.view")]
    public class SuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public SuppliersController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        private IQueryable<Supplier> BuildSuppliersQuery(
            string? search,
            int? branchId,
            SupplierBalanceFilter balanceFilter,
            IReadOnlyCollection<int> userBranchIds,
            int? supplierTypeId)
        {
            var suppliersQuery = _context.Suppliers
                .AsNoTracking()
                .Include(s => s.Account)
                    .ThenInclude(a => a.Currency)
                .Include(s => s.CreatedBy)
                .Include(s => s.SupplierBranches)
                    .ThenInclude(sb => sb.Branch)
                .Include(s => s.SupplierType)
                .Where(s => s.IsActive)
                .AsQueryable();

            if (userBranchIds.Count > 0)
            {
                suppliersQuery = suppliersQuery.Where(s =>
                    s.SupplierBranches.Any(sb => userBranchIds.Contains(sb.BranchId)));
            }

            if (!string.IsNullOrWhiteSpace(search))
            {
                suppliersQuery = suppliersQuery.Where(s =>
                    EF.Functions.Like(s.NameAr, $"%{search}%") ||
                    (s.NameEn != null && EF.Functions.Like(s.NameEn, $"%{search}%")) ||
                    (s.Phone != null && EF.Functions.Like(s.Phone, $"%{search}%")) ||
                    (s.Email != null && EF.Functions.Like(s.Email, $"%{search}%")));
            }

            if (branchId.HasValue)
            {
                suppliersQuery = suppliersQuery.Where(s =>
                    s.SupplierBranches.Any(sb => sb.BranchId == branchId.Value));
            }

            if (supplierTypeId.HasValue)
            {
                suppliersQuery = suppliersQuery.Where(s => s.SupplierTypeId == supplierTypeId.Value);
            }

            suppliersQuery = balanceFilter switch
            {
                SupplierBalanceFilter.Positive => suppliersQuery.Where(s => s.Account != null && s.Account.CurrentBalance > 0),
                SupplierBalanceFilter.Negative => suppliersQuery.Where(s => s.Account != null && s.Account.CurrentBalance < 0),
                SupplierBalanceFilter.Zero => suppliersQuery.Where(s => s.Account != null && s.Account.CurrentBalance == 0),
                _ => suppliersQuery
            };

            return suppliersQuery;
        }

        // GET: Suppliers
        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == user.Id)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            var branchOptions = await _context.Branches
                .Where(b => b.IsActive && (userBranchIds.Count == 0 || userBranchIds.Contains(b.Id)))
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(b.NameAr) ? b.Code : b.NameAr
                })
                .ToListAsync();

            var supplierTypeOptions = await _context.SupplierTypes
                .Where(t => t.IsActive)
                .OrderBy(t => t.Name)
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Name
                })
                .ToListAsync();

            var suppliers = await BuildSuppliersQuery(
                    search: null,
                    branchId: null,
                    balanceFilter: SupplierBalanceFilter.All,
                    userBranchIds: userBranchIds,
                    supplierTypeId: null)
                .ToListAsync();

            var balances = suppliers
                .Select(s => s.Account != null ? s.Account.CurrentBalance * -1 : 0m)
                .ToList();

            var model = new SuppliersIndexViewModel
            {
                BranchOptions = branchOptions,
                SupplierTypeOptions = supplierTypeOptions,
                PositiveBalanceTotal = balances.Where(b => b > 0).Sum(),
                NegativeBalanceTotal = balances.Where(b => b < 0).Sum()
            };

            return View(model);
        }

        [HttpPost]
        public async Task<IActionResult> UrlDatasource([FromBody] DataManagerRequest dm)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == user.Id)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            var branchId = ParseNullableInt(ReadRequestParameter(dm, "branchId"));
            var supplierTypeId = ParseNullableInt(ReadRequestParameter(dm, "supplierTypeId"));
            var balanceFilter = ParseBalanceFilter(ReadRequestParameter(dm, "balanceFilter"));

            var suppliers = await BuildSuppliersQuery(
                    search: null,
                    branchId: branchId,
                    balanceFilter: balanceFilter,
                    userBranchIds: userBranchIds,
                    supplierTypeId: supplierTypeId)
                .ToListAsync();

            var dataSource = suppliers.Select(s =>
            {
                var permissions = Enum.GetValues(typeof(SupplierAuthorization))
                    .Cast<SupplierAuthorization>()
                    .Where(a => a != SupplierAuthorization.None && s.AuthorizedOperations.HasFlag(a))
                    .Select(a => a.GetDisplayName())
                    .ToList();

                var branchNames = s.SupplierBranches != null && s.SupplierBranches.Any()
                    ? s.SupplierBranches
                        .Where(sb => sb.Branch != null)
                        .Select(sb => !string.IsNullOrWhiteSpace(sb.Branch!.NameAr)
                            ? sb.Branch!.NameAr
                            : (sb.Branch!.NameEn ?? sb.Branch!.Code))
                        .ToList()
                    : new List<string>();

                var createdByName = s.CreatedBy != null
                    ? (!string.IsNullOrWhiteSpace(s.CreatedBy.FullName)
                        ? s.CreatedBy.FullName
                        : s.CreatedBy.UserName ?? string.Empty)
                    : string.Empty;

                return new SupplierGridItemViewModel
                {
                    Id = s.Id,
                    Name = s.NameAr,
                    Phone = string.IsNullOrWhiteSpace(s.Phone) ? "-" : s.Phone!,
                    Email = string.IsNullOrWhiteSpace(s.Email) ? "-" : s.Email!,
                    SupplierType = s.SupplierType?.Name ?? "-",
                    Permissions = permissions.Count == 0 ? "-" : string.Join("، ", permissions),
                    Branches = branchNames.Count == 0 ? "-" : string.Join("، ", branchNames),
                    CreatedBy = string.IsNullOrWhiteSpace(createdByName) ? "-" : createdByName,
                    CreatedAt = s.CreatedAt,
                    Balance = s.Account != null ? s.Account.CurrentBalance * -1 : 0m,
                    CurrencyCode = s.Account?.Currency?.Code ?? string.Empty,
                    AccountId = s.AccountId
                };
            });

            var operation = new DataOperations();

            if (dm.Search != null && dm.Search.Count > 0)
            {
                dataSource = operation.PerformSearching(dataSource, dm.Search);
            }

            if (dm.Where != null && dm.Where.Count > 0)
            {
                dataSource = operation.PerformFiltering(dataSource, dm.Where, dm.Where[0].Operator);
            }

            var count = dataSource.Count();

            if (dm.Sorted != null && dm.Sorted.Count > 0)
            {
                dataSource = operation.PerformSorting(dataSource, dm.Sorted);
            }

            if (dm.Skip != 0)
            {
                dataSource = operation.PerformSkip(dataSource, dm.Skip);
            }

            if (dm.Take != 0)
            {
                dataSource = operation.PerformTake(dataSource, dm.Take);
            }

            return dm.RequiresCounts ? Json(new { result = dataSource, count }) : Json(dataSource);
        }

        [HttpGet]
        public async Task<IActionResult> GetSupplierTotals(
            int? branchId,
            int? supplierTypeId,
            SupplierBalanceFilter balanceFilter = SupplierBalanceFilter.All)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == user.Id)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            var suppliers = await BuildSuppliersQuery(null, branchId, balanceFilter, userBranchIds, supplierTypeId)
                .ToListAsync();

            var balances = suppliers
                .Select(s => s.Account != null ? s.Account.CurrentBalance * -1 : 0m)
                .ToList();

            return Json(new
            {
                positive = balances.Where(b => b > 0).Sum(),
                negative = balances.Where(b => b < 0).Sum()
            });
        }

        private static int? ParseNullableInt(object? value)
        {
            if (value == null)
            {
                return null;
            }

            return int.TryParse(value.ToString(), out var parsed) ? parsed : null;
        }

        private static SupplierBalanceFilter ParseBalanceFilter(object? value)
        {
            if (value == null)
            {
                return SupplierBalanceFilter.All;
            }

            return Enum.TryParse<SupplierBalanceFilter>(value.ToString(), out var parsed)
                ? parsed
                : SupplierBalanceFilter.All;
        }

        private string? ReadRequestParameter(DataManagerRequest dm, string key)
        {
            static string? TryReadFromDictionary(object? dictionary, string dictionaryKey)
            {
                if (dictionary is IDictionary<string, object?> genericDict && genericDict.TryGetValue(dictionaryKey, out var value))
                {
                    return value?.ToString();
                }

                if (dictionary is IDictionary nonGenericDict && nonGenericDict.Contains(dictionaryKey))
                {
                    return nonGenericDict[dictionaryKey]?.ToString();
                }

                if (dictionary is JsonElement element && element.ValueKind == JsonValueKind.Object && element.TryGetProperty(dictionaryKey, out var property))
                {
                    return property.ValueKind switch
                    {
                        JsonValueKind.String => property.GetString(),
                        JsonValueKind.Number => property.GetRawText(),
                        JsonValueKind.True or JsonValueKind.False => property.GetBoolean().ToString(),
                        JsonValueKind.Null => null,
                        _ => property.GetRawText()
                    };
                }

                return null;
            }

            var paramsProperty = dm.GetType().GetProperty("Params");
            if (paramsProperty != null)
            {
                var value = TryReadFromDictionary(paramsProperty.GetValue(dm), key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            var additionalParamsProperty = dm.GetType().GetProperty("AdditionalParams");
            if (additionalParamsProperty != null)
            {
                var value = TryReadFromDictionary(additionalParamsProperty.GetValue(dm), key);
                if (!string.IsNullOrWhiteSpace(value))
                {
                    return value;
                }
            }

            if (Request.Query.TryGetValue(key, out var queryValues))
            {
                var queryValue = queryValues.ToString();
                if (!string.IsNullOrWhiteSpace(queryValue))
                {
                    return queryValue;
                }
            }

            return null;
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            string? search,
            int? branchId,
            int? supplierTypeId,
            SupplierBalanceFilter balanceFilter = SupplierBalanceFilter.All)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == user.Id)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            var suppliers = await BuildSuppliersQuery(search, branchId, balanceFilter, userBranchIds, supplierTypeId)
                .OrderBy(s => s.NameAr)
                .ThenBy(s => s.Id)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Suppliers");

            worksheet.Cell(1, 1).Value = "اسم المورد";
            worksheet.Cell(1, 2).Value = "رقم الهاتف";
            worksheet.Cell(1, 3).Value = "البريد الإلكتروني";
            worksheet.Cell(1, 4).Value = "الصلاحيات";
            worksheet.Cell(1, 5).Value = "الفروع";
            worksheet.Cell(1, 6).Value = "المستخدم";
            worksheet.Cell(1, 7).Value = "تاريخ الإنشاء";
            worksheet.Cell(1, 8).Value = "رصيد المورد";
            worksheet.Cell(1, 9).Value = "العملة";
            worksheet.Cell(1, 10).Value = "نوع المورد";

            worksheet.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var supplier in suppliers)
            {
                worksheet.Cell(row, 1).Value = supplier.NameAr;
                worksheet.Cell(row, 2).Value = supplier.Phone ?? string.Empty;
                worksheet.Cell(row, 3).Value = supplier.Email ?? string.Empty;

                var permissions = Enum.GetValues(typeof(SupplierAuthorization))
                    .Cast<SupplierAuthorization>()
                    .Where(a => a != SupplierAuthorization.None && supplier.AuthorizedOperations.HasFlag(a))
                    .Select(a => a.GetDisplayName())
                    .ToList();
                worksheet.Cell(row, 4).Value = permissions.Count == 0 ? "-" : string.Join("، ", permissions);

                var branchNames = supplier.SupplierBranches != null && supplier.SupplierBranches.Any()
                    ? supplier.SupplierBranches
                        .Where(sb => sb.Branch != null)
                        .Select(sb => !string.IsNullOrWhiteSpace(sb.Branch!.NameAr)
                            ? sb.Branch!.NameAr
                            : (sb.Branch!.NameEn ?? sb.Branch!.Code))
                        .ToList()
                    : new List<string>();
                worksheet.Cell(row, 5).Value = branchNames.Count == 0 ? "-" : string.Join("، ", branchNames);

                worksheet.Cell(row, 6).Value = supplier.CreatedBy?.FullName ?? "-";
                worksheet.Cell(row, 7).Value = supplier.CreatedAt;
                worksheet.Cell(row, 7).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";

                if (supplier.Account != null)
                {
                    worksheet.Cell(row, 8).Value = supplier.Account.CurrentBalance;
                    worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
                    worksheet.Cell(row, 9).Value = supplier.Account.Currency?.Code ?? string.Empty;
                }
                else
                {
                    worksheet.Cell(row, 8).Value = "-";
                    worksheet.Cell(row, 9).Value = string.Empty;
                }

                worksheet.Cell(row, 10).Value = supplier.SupplierType?.Name ?? string.Empty;

                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"Suppliers_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        // GET: Suppliers/Create
        [Authorize(Policy = "suppliers.create")]
        public IActionResult Create()
        {
            var model = new SupplierFormViewModel
            {
                SelectedAuthorizations = new List<SupplierAuthorization>
                {
                    SupplierAuthorization.PaymentVoucher,
                    SupplierAuthorization.ReceiptVoucher
                },
                SelectedBranchIds = _context.Branches
                    .Where(b => b.IsActive)
                    .Select(b => b.Id)
                    .ToList()
            };


            return View(BuildFormViewModel(model));
        }

        // POST: Suppliers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "suppliers.create")]
        public async Task<IActionResult> Create(SupplierFormViewModel model)
        {
            if (model.SelectedAuthorizations == null || !model.SelectedAuthorizations.Any())
            {
                ModelState.AddModelError(nameof(model.SelectedAuthorizations), "يرجى اختيار صلاحية واحدة على الأقل.");
            }

            model.SelectedAuthorizations ??= new List<SupplierAuthorization>();
            model.SelectedBranchIds ??= new List<int>();

            var branchesExist = await _context.Branches.AnyAsync();
            var validBranchIds = model.SelectedBranchIds.Any()
                ? await _context.Branches
                    .Where(b => model.SelectedBranchIds.Contains(b.Id))
                    .Select(b => b.Id)
                    .ToListAsync()
                : new List<int>();

            if (!model.SelectedBranchIds.Any() && branchesExist)
            {
                ModelState.AddModelError(nameof(model.SelectedBranchIds), "يرجى اختيار فرع واحد على الأقل.");
            }
            else if (!validBranchIds.Any() && branchesExist)
            {
                ModelState.AddModelError(nameof(model.SelectedBranchIds), "يرجى اختيار فرع واحد على الأقل.");
            }

            if (!await _context.SupplierTypes.AnyAsync(t => t.Id == model.SupplierTypeId && t.IsActive))
            {
                ModelState.AddModelError(nameof(model.SupplierTypeId), "نوع المورد المحدد غير صالح.");
            }

            if (ModelState.IsValid)
            {
                var user = await _userManager.GetUserAsync(User);
                if (user == null)
                {
                    return Challenge();
                }

                Account? parentAccount = null;
                var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "SuppliersParentAccountId");
                if (setting != null && int.TryParse(setting.Value, out var parentId))
                {
                    parentAccount = await _context.Accounts
                        .Include(a => a.Children)
                        .FirstOrDefaultAsync(a => a.Code == parentId.ToString());
                }

                string code;
                int level;
                int currencyId;
                if (parentAccount != null)
                {
                    code = await GenerateUniqueChildCodeAsync(parentAccount);
                    level = parentAccount.Level + 1;
                    currencyId = parentAccount.CurrencyId;
                }
                else
                {
                    code = await GenerateUniqueRootCodeAsync(AccountType.Liabilities);
                    level = 1;
                    currencyId = await _context.Currencies.Select(c => c.Id).FirstAsync();
                }

                var account = new Account
                {
                    Code = code,
                    NameAr = model.NameAr,
                    NameEn = model.NameEn,
                    AccountType = AccountType.Liabilities,
                    Nature = AccountNature.Credit,
                    Classification = AccountClassification.BalanceSheet,
                    SubClassification = AccountSubClassification.Liabilities,
                    CanPostTransactions = true,
                    ParentId = parentAccount?.Id,
                    Level = level,
                    CurrencyId = currencyId
                };

                _context.Accounts.Add(account);
                await _context.SaveChangesAsync();

                var supplier = new Supplier
                {
                    NameAr = model.NameAr,
                    NameEn = model.NameEn,
                    Phone = model.Phone,
                    Email = model.Email,
                    IsActive = model.IsActive,
                    SupplierTypeId = model.SupplierTypeId,
                    AuthorizedOperations = CombineAuthorizations(model.SelectedAuthorizations),
                    AccountId = account.Id,
                    CreatedById = user.Id,
                    CreatedAt = DateTime.Now
                };

                foreach (var branchId in validBranchIds.Distinct())
                {
                    supplier.SupplierBranches.Add(new SupplierBranch
                    {
                        BranchId = branchId
                    });
                }

                _context.Suppliers.Add(supplier);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }

            return View(BuildFormViewModel(model));
        }

        // GET: Suppliers/Edit/5
        [Authorize(Policy = "suppliers.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var supplier = await _context.Suppliers
                .Include(s => s.Account)
                .Include(s => s.SupplierBranches)
                    .ThenInclude(sb => sb.Branch)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (supplier == null)
            {
                return NotFound();
            }

            var model = new SupplierFormViewModel
            {
                Id = supplier.Id,
                NameAr = supplier.NameAr,
                NameEn = supplier.NameEn,
                Phone = supplier.Phone,
                Email = supplier.Email,
                IsActive = supplier.IsActive,
                SupplierTypeId = supplier.SupplierTypeId,
                SelectedAuthorizations = SplitAuthorizations(supplier.AuthorizedOperations),
                SelectedBranchIds = supplier.SupplierBranches.Select(sb => sb.BranchId).ToList()
            };

            return View(BuildFormViewModel(model));
        }

        // POST: Suppliers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "suppliers.edit")]
        public async Task<IActionResult> Edit(int id, SupplierFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (model.SelectedAuthorizations == null || !model.SelectedAuthorizations.Any())
            {
                ModelState.AddModelError(nameof(model.SelectedAuthorizations), "يرجى اختيار صلاحية واحدة على الأقل.");
            }

            model.SelectedAuthorizations ??= new List<SupplierAuthorization>();
            model.SelectedBranchIds ??= new List<int>();

            var branchesExist = await _context.Branches.AnyAsync();
            var validBranchIds = model.SelectedBranchIds.Any()
                ? await _context.Branches
                    .Where(b => model.SelectedBranchIds.Contains(b.Id))
                    .Select(b => b.Id)
                    .ToListAsync()
                : new List<int>();

            if (!model.SelectedBranchIds.Any() && branchesExist)
            {
                ModelState.AddModelError(nameof(model.SelectedBranchIds), "يرجى اختيار فرع واحد على الأقل.");
            }
            else if (!validBranchIds.Any() && branchesExist)
            {
                ModelState.AddModelError(nameof(model.SelectedBranchIds), "يرجى اختيار فرع واحد على الأقل.");
            }

            if (ModelState.IsValid)
            {
                var supplier = await _context.Suppliers
                    .Include(s => s.Account)
                    .Include(s => s.SupplierBranches)
                    .FirstOrDefaultAsync(s => s.Id == id);
                if (supplier == null)
                {
                    return NotFound();
                }

                var supplierTypeExists = await _context.SupplierTypes
                    .AnyAsync(t => t.Id == model.SupplierTypeId && t.IsActive);
                if (!supplierTypeExists)
                {
                    ModelState.AddModelError(nameof(model.SupplierTypeId), "نوع المورد المحدد غير صالح.");
                    return View(BuildFormViewModel(model));
                }

                supplier.NameAr = model.NameAr;
                supplier.NameEn = model.NameEn;
                supplier.Phone = model.Phone;
                supplier.Email = model.Email;
                supplier.IsActive = model.IsActive;
                supplier.SupplierTypeId = model.SupplierTypeId;
                supplier.AuthorizedOperations = CombineAuthorizations(model.SelectedAuthorizations);

                var distinctBranchIds = validBranchIds.Distinct().ToList();

                var branchesToRemove = supplier.SupplierBranches
                    .Where(sb => !distinctBranchIds.Contains(sb.BranchId))
                    .ToList();

                if (branchesToRemove.Any())
                {
                    foreach (var supplierBranch in branchesToRemove)
                    {
                        supplier.SupplierBranches.Remove(supplierBranch);
                    }

                    _context.SupplierBranches.RemoveRange(branchesToRemove);
                }

                var existingBranchIds = supplier.SupplierBranches
                    .Select(sb => sb.BranchId)
                    .ToHashSet();

                foreach (var branchId in distinctBranchIds)
                {
                    if (!existingBranchIds.Contains(branchId))
                    {
                        supplier.SupplierBranches.Add(new SupplierBranch
                        {
                            SupplierId = supplier.Id,
                            BranchId = branchId
                        });
                    }
                }

                if (supplier.Account != null)
                {
                    supplier.Account.NameAr = model.NameAr;
                    supplier.Account.NameEn = model.NameEn;
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(BuildFormViewModel(model));
        }

        // POST: Suppliers/Delete/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "suppliers.delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var supplier = await _context.Suppliers
                .Include(s => s.Account)
                    .ThenInclude(a => a.JournalEntryLines)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (supplier == null)
            {
                return NotFound();
            }

            if (supplier.Account != null && supplier.Account.JournalEntryLines.Any())
            {
                TempData["Error"] = "لا يمكن حذف المورد لوجود معاملات مرتبطة به";
                return RedirectToAction(nameof(Index));
            }

            if (supplier.Account != null)
            {
                _context.Accounts.Remove(supplier.Account);
            }
            _context.Suppliers.Remove(supplier);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حذف المورد بنجاح";
            return RedirectToAction(nameof(Index));
        }

        private async Task<string> GenerateUniqueRootCodeAsync(AccountType accountType)
        {
            var lastRootCode = await _context.Accounts
                .Where(a => a.ParentId == null && a.AccountType == accountType)
                .OrderByDescending(a => a.Code)
                .Select(a => a.Code)
                .FirstOrDefaultAsync();

            var newCode = GenerateRootCode(accountType, lastRootCode);
            while (await _context.Accounts.AnyAsync(a => a.Code == newCode))
            {
                lastRootCode = newCode;
                newCode = GenerateRootCode(accountType, lastRootCode);
            }

            return newCode;
        }

        private async Task<string> GenerateUniqueChildCodeAsync(Account parentAccount)
        {
            var lastChildCode = await _context.Accounts
                .Where(a => a.ParentId == parentAccount.Id)
                .OrderByDescending(a => a.Code)
                .Select(a => a.Code)
                .FirstOrDefaultAsync();

            var newCode = GenerateChildCode(parentAccount.Code, lastChildCode);
            while (await _context.Accounts.AnyAsync(a => a.Code == newCode))
            {
                lastChildCode = newCode;
                newCode = GenerateChildCode(parentAccount.Code, lastChildCode);
            }

            return newCode;
        }

        private static string GenerateRootCode(AccountType accountType, string? lastRootCode)
        {
            var baseCode = ((int)accountType).ToString();
            if (string.IsNullOrEmpty(lastRootCode))
                return baseCode;

            if (int.TryParse(lastRootCode, out var rootNumber))
                return (rootNumber + 1).ToString();

            return baseCode + "1";
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

        private SupplierFormViewModel BuildFormViewModel(SupplierFormViewModel model)
        {
            model.SelectedAuthorizations ??= new List<SupplierAuthorization>();
            model.SelectedBranchIds ??= new List<int>();

            model.AuthorizationOptions = Enum.GetValues(typeof(SupplierAuthorization))
                .Cast<SupplierAuthorization>()
                .Where(a => a != SupplierAuthorization.None)
                .Select(a => new SelectListItem
                {
                    Value = ((int)a).ToString(),
                    Text = a.GetDisplayName(),
                    Selected = model.SelectedAuthorizations.Contains(a)
                })
                .ToList();

            model.BranchOptions = _context.Branches
                .Where(b => b.IsActive || model.SelectedBranchIds.Contains(b.Id))
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = !string.IsNullOrWhiteSpace(b.NameAr) ? b.NameAr : (b.NameEn ?? b.Code),
                    Selected = model.SelectedBranchIds.Contains(b.Id)
                })
                .ToList();

            model.SupplierTypeOptions = _context.SupplierTypes
                .Where(t => t.IsActive || t.Id == model.SupplierTypeId)
                .OrderBy(t => t.Name)
                .Select(t => new SelectListItem
                {
                    Value = t.Id.ToString(),
                    Text = t.Name,
                    Selected = t.Id == model.SupplierTypeId
                })
                .ToList();

            if (model.SupplierTypeId == 0 && model.SupplierTypeOptions.Any())
            {
                model.SupplierTypeId = int.Parse(model.SupplierTypeOptions.First().Value);
                model.SupplierTypeOptions.First().Selected = true;
            }

            return model;
        }

        private static SupplierAuthorization CombineAuthorizations(IEnumerable<SupplierAuthorization> authorizations)
        {
            var result = SupplierAuthorization.None;

            foreach (var authorization in authorizations)
            {
                result |= authorization;
            }

            return result == SupplierAuthorization.None
                ? SupplierAuthorization.None
                : result;
        }

        private static List<SupplierAuthorization> SplitAuthorizations(SupplierAuthorization authorizations)
        {
            return Enum.GetValues(typeof(SupplierAuthorization))
                .Cast<SupplierAuthorization>()
                .Where(a => a != SupplierAuthorization.None && authorizations.HasFlag(a))
                .ToList();
        }
    }
}
