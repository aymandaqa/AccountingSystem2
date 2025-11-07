using System;
using System.Collections.Generic;
using System.Linq;
using AccountingSystem.Data;
using AccountingSystem.Extensions;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

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

        // GET: Suppliers
        public async Task<IActionResult> Index(string? search)
        {
            var suppliersQuery = _context.Suppliers
                .Include(s => s.Account)
                    .ThenInclude(a => a.Currency)
                .Include(s => s.CreatedBy)
                .Include(s => s.SupplierBranches)
                    .ThenInclude(sb => sb.Branch)
                .Include(s => s.CreatedBy)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                suppliersQuery = suppliersQuery.Where(s =>
                    EF.Functions.Like(s.NameAr, $"%{search}%") ||
                    (s.NameEn != null && EF.Functions.Like(s.NameEn, $"%{search}%")) ||
                    (s.Phone != null && EF.Functions.Like(s.Phone, $"%{search}%")) ||
                    (s.Email != null && EF.Functions.Like(s.Email, $"%{search}%")));
            }

            var suppliers = await suppliersQuery
                .OrderBy(s => s.NameAr)
                .ThenBy(s => s.Id)
                .ToListAsync();

            ViewBag.Search = search;

            return View(suppliers);
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
                    var lastChildCode = parentAccount.Children
                        .OrderByDescending(c => c.Code)
                        .Select(c => c.Code)
                        .FirstOrDefault();
                    code = GenerateChildCode(parentAccount.Code, lastChildCode);
                    level = parentAccount.Level + 1;
                    currencyId = parentAccount.CurrencyId;
                }
                else
                {
                    var baseCode = ((int)AccountType.Liabilities).ToString();
                    var lastRootCode = await _context.Accounts
                        .Where(a => a.ParentId == null && a.AccountType == AccountType.Liabilities)
                        .OrderByDescending(a => a.Code)
                        .Select(a => a.Code)
                        .FirstOrDefaultAsync();

                    if (string.IsNullOrEmpty(lastRootCode))
                        code = baseCode;
                    else if (int.TryParse(lastRootCode, out var rootNumber))
                        code = (rootNumber + 1).ToString();
                    else
                        code = baseCode + "1";
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

                supplier.NameAr = model.NameAr;
                supplier.NameEn = model.NameEn;
                supplier.Phone = model.Phone;
                supplier.Email = model.Email;
                supplier.IsActive = model.IsActive;
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
