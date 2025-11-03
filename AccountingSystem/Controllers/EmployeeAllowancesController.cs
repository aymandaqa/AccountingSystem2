using System;
using System.Globalization;
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
    [Authorize(Policy = "employees.edit")]
    public class EmployeeAllowancesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string StatusMessageKey = "StatusMessage";

        public EmployeeAllowancesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var culture = new CultureInfo("ar");
            var items = await _context.EmployeeAllowances
                .AsNoTracking()
                .Include(a => a.Employee).ThenInclude(e => e.Branch)
                .Include(a => a.AllowanceType).ThenInclude(t => t.Account)
                .OrderByDescending(a => a.Year)
                .ThenByDescending(a => a.Month)
                .ThenBy(a => a.Employee.Name)
                .ThenBy(a => a.AllowanceType.Name)
                .Select(a => new EmployeeAllowanceListItemViewModel
                {
                    Id = a.Id,
                    EmployeeName = a.Employee.Name,
                    EmployeeBranch = a.Employee.Branch.NameAr,
                    AllowanceTypeName = a.AllowanceType.Name,
                    AccountDisplay = a.AllowanceType.Account != null
                        ? $"{a.AllowanceType.Account.Code} - {a.AllowanceType.Account.NameAr ?? a.AllowanceType.Account.NameEn ?? string.Empty}"
                        : string.Empty,
                    Amount = a.Amount,
                    Description = a.Description,
                    IsActive = a.IsActive,
                    Year = a.Year,
                    Month = a.Month,
                })
                .ToListAsync();

            foreach (var item in items)
            {
                item.PeriodName = item.Year > 0 && item.Month > 0
                    ? new DateTime(item.Year, item.Month, 1).ToString("MMMM yyyy", culture)
                    : string.Empty;
            }

            ViewBag.StatusMessage = TempData[StatusMessageKey]?.ToString();
            return View(items);
        }

        public async Task<IActionResult> Create()
        {
            var model = new EmployeeAllowanceFormViewModel
            {
                Employees = await GetEmployeeSelectListAsync(),
                AllowanceTypes = await GetAllowanceTypeSelectListAsync(),
                Period = DateTime.Today.ToString("yyyy-MM")
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeAllowanceFormViewModel model)
        {
            model.Employees = await GetEmployeeSelectListAsync();
            model.AllowanceTypes = await GetAllowanceTypeSelectListAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!TryParsePeriod(model.Period, out var year, out var month))
            {
                ModelState.AddModelError(nameof(model.Period), "يرجى اختيار شهر صالح");
                return View(model);
            }

            var employee = await _context.Employees
                .Include(e => e.Account)
                .FirstOrDefaultAsync(e => e.Id == model.EmployeeId);

            if (employee == null)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "الموظف المحدد غير موجود");
                return View(model);
            }

            var allowanceType = await _context.AllowanceTypes
                .Include(a => a.Account)
                .FirstOrDefaultAsync(a => a.Id == model.AllowanceTypeId && a.IsActive);

            if (allowanceType == null)
            {
                ModelState.AddModelError(nameof(model.AllowanceTypeId), "نوع البدل المحدد غير موجود أو غير نشط");
                return View(model);
            }

            if (employee.Account == null)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "لا يوجد حساب مرتبط بالموظف");
                return View(model);
            }

            if (allowanceType.Account != null && allowanceType.Account.CurrencyId != employee.Account.CurrencyId)
            {
                ModelState.AddModelError(nameof(model.AllowanceTypeId), "عملة حساب البدل لا تطابق عملة حساب الموظف");
                return View(model);
            }

            var amount = Math.Round(model.Amount, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0)
            {
                ModelState.AddModelError(nameof(model.Amount), "يجب أن يكون مبلغ البدل أكبر من صفر");
                return View(model);
            }

            var allowance = new EmployeeAllowance
            {
                EmployeeId = employee.Id,
                AllowanceTypeId = allowanceType.Id,
                Amount = amount,
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now,
                Year = year,
                Month = month
            };

            _context.EmployeeAllowances.Add(allowance);
            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم إضافة بدل الموظف بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var allowance = await _context.EmployeeAllowances
                .AsNoTracking()
                .FirstOrDefaultAsync(a => a.Id == id);

            if (allowance == null)
            {
                return NotFound();
            }

            var model = new EmployeeAllowanceFormViewModel
            {
                Id = allowance.Id,
                EmployeeId = allowance.EmployeeId,
                AllowanceTypeId = allowance.AllowanceTypeId,
                Amount = allowance.Amount,
                Description = allowance.Description,
                IsActive = allowance.IsActive,
                Period = $"{allowance.Year:D4}-{allowance.Month:D2}",
                Employees = await GetEmployeeSelectListAsync(),
                AllowanceTypes = await GetAllowanceTypeSelectListAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EmployeeAllowanceFormViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            model.Employees = await GetEmployeeSelectListAsync();
            model.AllowanceTypes = await GetAllowanceTypeSelectListAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!TryParsePeriod(model.Period, out var year, out var month))
            {
                ModelState.AddModelError(nameof(model.Period), "يرجى اختيار شهر صالح");
                return View(model);
            }

            var allowance = await _context.EmployeeAllowances
                .Include(a => a.Employee).ThenInclude(e => e.Account)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (allowance == null)
            {
                return NotFound();
            }

            var employee = await _context.Employees
                .Include(e => e.Account)
                .FirstOrDefaultAsync(e => e.Id == model.EmployeeId);

            if (employee == null)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "الموظف المحدد غير موجود");
                return View(model);
            }

            var allowanceType = await _context.AllowanceTypes
                .Include(a => a.Account)
                .FirstOrDefaultAsync(a => a.Id == model.AllowanceTypeId && a.IsActive);

            if (allowanceType == null)
            {
                ModelState.AddModelError(nameof(model.AllowanceTypeId), "نوع البدل المحدد غير موجود أو غير نشط");
                return View(model);
            }

            if (employee.Account == null)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "لا يوجد حساب مرتبط بالموظف");
                return View(model);
            }

            if (allowanceType.Account != null && allowanceType.Account.CurrencyId != employee.Account.CurrencyId)
            {
                ModelState.AddModelError(nameof(model.AllowanceTypeId), "عملة حساب البدل لا تطابق عملة حساب الموظف");
                return View(model);
            }

            var amount = Math.Round(model.Amount, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0)
            {
                ModelState.AddModelError(nameof(model.Amount), "يجب أن يكون مبلغ البدل أكبر من صفر");
                return View(model);
            }

            allowance.EmployeeId = employee.Id;
            allowance.AllowanceTypeId = allowanceType.Id;
            allowance.Amount = amount;
            allowance.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            allowance.IsActive = model.IsActive;
            allowance.Year = year;
            allowance.Month = month;
            allowance.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم تحديث بدل الموظف بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var allowance = await _context.EmployeeAllowances
                .FirstOrDefaultAsync(a => a.Id == id);

            if (allowance == null)
            {
                return NotFound();
            }

            _context.EmployeeAllowances.Remove(allowance);
            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم حذف بدل الموظف بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<List<SelectListItem>> GetEmployeeSelectListAsync()
        {
            return await _context.Employees
                .AsNoTracking()
                .Where(e => e.IsActive)
                .OrderBy(e => e.Name)
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = $"{e.Name} - {e.Branch.NameAr}"
                })
                .ToListAsync();
        }

        private async Task<List<SelectListItem>> GetAllowanceTypeSelectListAsync()
        {
            return await _context.AllowanceTypes
                .AsNoTracking()
                .Where(a => a.IsActive)
                .OrderBy(a => a.Name)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = a.Name
                })
                .ToListAsync();
        }

        private static bool TryParsePeriod(string? period, out int year, out int month)
        {
            year = 0;
            month = 0;
            if (string.IsNullOrWhiteSpace(period))
            {
                return false;
            }

            if (DateTime.TryParseExact(period, "yyyy-MM", CultureInfo.InvariantCulture, DateTimeStyles.None, out var date))
            {
                year = date.Year;
                month = date.Month;
                return true;
            }

            return false;
        }
    }
}
