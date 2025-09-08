using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "suppliers.view")]
    public class SuppliersController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SuppliersController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Suppliers
        public async Task<IActionResult> Index()
        {
            var suppliers = await _context.Suppliers
                .Include(s => s.Account)
                .OrderBy(s => s.NameAr)
                .ToListAsync();
            return View(suppliers);
        }

        // GET: Suppliers/Create
        [Authorize(Policy = "suppliers.create")]
        public IActionResult Create()
        {
            return View(new Supplier());
        }

        // POST: Suppliers/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "suppliers.create")]
        public async Task<IActionResult> Create(Supplier model)
        {
            if (ModelState.IsValid)
            {
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

                model.AccountId = account.Id;
                _context.Suppliers.Add(model);
                await _context.SaveChangesAsync();

                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        // GET: Suppliers/Edit/5
        [Authorize(Policy = "suppliers.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var supplier = await _context.Suppliers
                .Include(s => s.Account)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (supplier == null)
            {
                return NotFound();
            }
            return View(supplier);
        }

        // POST: Suppliers/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "suppliers.edit")]
        public async Task<IActionResult> Edit(int id, Supplier model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var supplier = await _context.Suppliers
                    .Include(s => s.Account)
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

                if (supplier.Account != null)
                {
                    supplier.Account.NameAr = model.NameAr;
                    supplier.Account.NameEn = model.NameEn;
                }

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            return View(model);
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
    }
}
