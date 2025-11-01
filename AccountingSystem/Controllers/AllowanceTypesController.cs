using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "payroll.process")]
    public class AllowanceTypesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string StatusMessageKey = "StatusMessage";

        public AllowanceTypesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _context.AllowanceTypes
                .AsNoTracking()
                .Include(a => a.Account)
                .OrderBy(a => a.Name)
                .Select(a => new AllowanceTypeListItemViewModel
                {
                    Id = a.Id,
                    Name = a.Name,
                    Description = a.Description,
                    IsActive = a.IsActive,
                    AccountDisplay = a.Account != null
                        ? $"{a.Account.Code} - {a.Account.NameAr ?? a.Account.NameEn ?? string.Empty}"
                        : string.Empty
                })
                .ToListAsync();

            ViewBag.StatusMessage = TempData[StatusMessageKey]?.ToString();
            return View(items);
        }

        public async Task<IActionResult> Create()
        {
            var model = new AllowanceTypeFormViewModel
            {
                Accounts = await GetAccountSelectListAsync()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(AllowanceTypeFormViewModel model)
        {
            model.Accounts = await GetAccountSelectListAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == model.AccountId && a.IsActive && a.CanPostTransactions);

            if (account == null)
            {
                ModelState.AddModelError(nameof(model.AccountId), "الحساب المحدد غير صالح أو غير نشط");
                return View(model);
            }

            var allowanceType = new AllowanceType
            {
                Name = model.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                AccountId = account.Id,
                IsActive = model.IsActive
            };

            _context.AllowanceTypes.Add(allowanceType);
            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم إنشاء نوع البدل بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var allowanceType = await _context.AllowanceTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (allowanceType == null)
            {
                return NotFound();
            }

            var model = new AllowanceTypeFormViewModel
            {
                Id = allowanceType.Id,
                Name = allowanceType.Name,
                Description = allowanceType.Description,
                AccountId = allowanceType.AccountId,
                IsActive = allowanceType.IsActive,
                Accounts = await GetAccountSelectListAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, AllowanceTypeFormViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            model.Accounts = await GetAccountSelectListAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var allowanceType = await _context.AllowanceTypes
                .FirstOrDefaultAsync(a => a.Id == id);

            if (allowanceType == null)
            {
                return NotFound();
            }

            var account = await _context.Accounts
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == model.AccountId && a.IsActive && a.CanPostTransactions);

            if (account == null)
            {
                ModelState.AddModelError(nameof(model.AccountId), "الحساب المحدد غير صالح أو غير نشط");
                return View(model);
            }

            allowanceType.Name = model.Name.Trim();
            allowanceType.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            allowanceType.AccountId = account.Id;
            allowanceType.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم تحديث نوع البدل بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var allowanceType = await _context.AllowanceTypes
                .Include(a => a.EmployeeAllowances)
                .Include(a => a.PayrollAllowances)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (allowanceType == null)
            {
                return NotFound();
            }

            if (allowanceType.EmployeeAllowances.Any() || allowanceType.PayrollAllowances.Any())
            {
                allowanceType.IsActive = false;
                await _context.SaveChangesAsync();
                TempData[StatusMessageKey] = "تم إلغاء تفعيل نوع البدل لوجود استخدامات مرتبطة به.";
            }
            else
            {
                _context.AllowanceTypes.Remove(allowanceType);
                await _context.SaveChangesAsync();
                TempData[StatusMessageKey] = "تم حذف نوع البدل بنجاح.";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task<List<SelectListItem>> GetAccountSelectListAsync()
        {
            return await _context.Accounts
                .AsNoTracking()
                .Where(a => a.IsActive && a.CanPostTransactions)
                .OrderBy(a => a.Code)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr ?? a.NameEn ?? string.Empty}"
                })
                .ToListAsync();
        }
    }
}
