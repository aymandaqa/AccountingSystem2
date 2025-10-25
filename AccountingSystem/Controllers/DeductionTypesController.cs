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
    public class DeductionTypesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string StatusMessageKey = "StatusMessage";

        public DeductionTypesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _context.DeductionTypes
                .AsNoTracking()
                .Include(d => d.Account)
                .OrderBy(d => d.Name)
                .Select(d => new DeductionTypeListItemViewModel
                {
                    Id = d.Id,
                    Name = d.Name,
                    Description = d.Description,
                    IsActive = d.IsActive,
                    AccountDisplay = d.Account != null
                        ? $"{d.Account.Code} - {d.Account.NameAr ?? d.Account.NameEn ?? string.Empty}"
                        : string.Empty
                })
                .ToListAsync();

            ViewBag.StatusMessage = TempData[StatusMessageKey]?.ToString();
            return View(items);
        }

        public async Task<IActionResult> Create()
        {
            var model = new DeductionTypeFormViewModel
            {
                Accounts = await GetAccountSelectListAsync()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DeductionTypeFormViewModel model)
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

            var deductionType = new DeductionType
            {
                Name = model.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                AccountId = account.Id,
                IsActive = model.IsActive
            };

            _context.DeductionTypes.Add(deductionType);
            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم إنشاء نوع الخصم بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var deductionType = await _context.DeductionTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id);

            if (deductionType == null)
            {
                return NotFound();
            }

            var model = new DeductionTypeFormViewModel
            {
                Id = deductionType.Id,
                Name = deductionType.Name,
                Description = deductionType.Description,
                AccountId = deductionType.AccountId,
                IsActive = deductionType.IsActive,
                Accounts = await GetAccountSelectListAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DeductionTypeFormViewModel model)
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

            var deductionType = await _context.DeductionTypes
                .FirstOrDefaultAsync(d => d.Id == id);

            if (deductionType == null)
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

            deductionType.Name = model.Name.Trim();
            deductionType.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            deductionType.AccountId = account.Id;
            deductionType.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم تحديث نوع الخصم بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var deductionType = await _context.DeductionTypes
                .Include(d => d.EmployeeDeductions)
                .Include(d => d.PayrollDeductions)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (deductionType == null)
            {
                return NotFound();
            }

            if (deductionType.EmployeeDeductions.Any() || deductionType.PayrollDeductions.Any())
            {
                deductionType.IsActive = false;
                await _context.SaveChangesAsync();
                TempData[StatusMessageKey] = "تم إلغاء تفعيل نوع الخصم لوجود استخدامات مرتبطة به.";
            }
            else
            {
                _context.DeductionTypes.Remove(deductionType);
                await _context.SaveChangesAsync();
                TempData[StatusMessageKey] = "تم حذف نوع الخصم بنجاح.";
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
