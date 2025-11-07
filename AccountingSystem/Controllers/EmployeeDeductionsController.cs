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
    public class EmployeeDeductionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string StatusMessageKey = "StatusMessage";

        public EmployeeDeductionsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var culture = CultureInfo.InvariantCulture;
            var items = await _context.EmployeeDeductions
                .AsNoTracking()
                .Include(d => d.Employee).ThenInclude(e => e.Branch)
                .Include(d => d.DeductionType).ThenInclude(t => t.Account)
                .OrderByDescending(d => d.Year)
                .ThenByDescending(d => d.Month)
                .ThenBy(d => d.Employee.Name)
                .ThenBy(d => d.DeductionType.Name)
                .Select(d => new EmployeeDeductionListItemViewModel
                {
                    Id = d.Id,
                    EmployeeName = d.Employee.Name,
                    EmployeeBranch = d.Employee.Branch.NameAr,
                    DeductionTypeName = d.DeductionType.Name,
                    AccountDisplay = d.DeductionType.Account != null
                        ? $"{d.DeductionType.Account.Code} - {d.DeductionType.Account.NameAr ?? d.DeductionType.Account.NameEn ?? string.Empty}"
                        : string.Empty,
                    Amount = d.Amount,
                    Description = d.Description,
                    IsActive = d.IsActive,
                    Year = d.Year,
                    Month = d.Month,
                })
                .ToListAsync();

            foreach (var item in items)
            {
                item.PeriodName = item.Year > 0 && item.Month > 0
                    ? new DateTime(item.Year, item.Month, 1).ToString("MM/yyyy", culture)
                    : string.Empty;
            }

            ViewBag.StatusMessage = TempData[StatusMessageKey]?.ToString();
            return View(items);
        }

        public async Task<IActionResult> Create(int? year, int? month, string? period)
        {
            var model = new EmployeeDeductionFormViewModel
            {
                Employees = await GetEmployeeSelectListAsync(),
                DeductionTypes = await GetDeductionTypeSelectListAsync(),
                Period = ResolveInitialPeriod(year, month, period)
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeDeductionFormViewModel model)
        {
            model.Employees = await GetEmployeeSelectListAsync();
            model.DeductionTypes = await GetDeductionTypeSelectListAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!TryParsePeriod(model.Period, out var year, out var month))
            {
                ModelState.AddModelError(nameof(model.Period), "يرجى اختيار شهر صالح");
                return View(model);
            }

            model.Period = $"{year:D4}-{month:D2}";

            var employee = await _context.Employees
                .Include(e => e.Account)
                .FirstOrDefaultAsync(e => e.Id == model.EmployeeId);

            if (employee == null)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "الموظف المحدد غير موجود");
                return View(model);
            }

            var deductionType = await _context.DeductionTypes
                .Include(d => d.Account)
                .FirstOrDefaultAsync(d => d.Id == model.DeductionTypeId && d.IsActive);

            if (deductionType == null)
            {
                ModelState.AddModelError(nameof(model.DeductionTypeId), "نوع الخصم المحدد غير موجود أو غير نشط");
                return View(model);
            }

            if (employee.Account == null)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "لا يوجد حساب مرتبط بالموظف");
                return View(model);
            }

            if (deductionType.Account != null && deductionType.Account.CurrencyId != employee.Account.CurrencyId)
            {
                ModelState.AddModelError(nameof(model.DeductionTypeId), "عملة حساب الخصم لا تطابق عملة حساب الموظف");
                return View(model);
            }

            var amount = Math.Round(model.Amount, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0)
            {
                ModelState.AddModelError(nameof(model.Amount), "يجب أن يكون مبلغ الخصم أكبر من صفر");
                return View(model);
            }

            var deduction = new EmployeeDeduction
            {
                EmployeeId = employee.Id,
                DeductionTypeId = deductionType.Id,
                Amount = amount,
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                IsActive = model.IsActive,
                CreatedAt = DateTime.Now,
                Year = year,
                Month = month
            };

            _context.EmployeeDeductions.Add(deduction);
            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم إضافة خصم الموظف بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var deduction = await _context.EmployeeDeductions
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id);

            if (deduction == null)
            {
                return NotFound();
            }

            var model = new EmployeeDeductionFormViewModel
            {
                Id = deduction.Id,
                EmployeeId = deduction.EmployeeId,
                DeductionTypeId = deduction.DeductionTypeId,
                Amount = deduction.Amount,
                Description = deduction.Description,
                IsActive = deduction.IsActive,
                Period = $"{deduction.Year:D4}-{deduction.Month:D2}",
                Employees = await GetEmployeeSelectListAsync(),
                DeductionTypes = await GetDeductionTypeSelectListAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EmployeeDeductionFormViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            model.Employees = await GetEmployeeSelectListAsync();
            model.DeductionTypes = await GetDeductionTypeSelectListAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            if (!TryParsePeriod(model.Period, out var year, out var month))
            {
                ModelState.AddModelError(nameof(model.Period), "يرجى اختيار شهر صالح");
                return View(model);
            }

            model.Period = $"{year:D4}-{month:D2}";

            var deduction = await _context.EmployeeDeductions
                .Include(d => d.Employee).ThenInclude(e => e.Account)
                .FirstOrDefaultAsync(d => d.Id == id);

            if (deduction == null)
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

            var deductionType = await _context.DeductionTypes
                .Include(d => d.Account)
                .FirstOrDefaultAsync(d => d.Id == model.DeductionTypeId && d.IsActive);

            if (deductionType == null)
            {
                ModelState.AddModelError(nameof(model.DeductionTypeId), "نوع الخصم المحدد غير موجود أو غير نشط");
                return View(model);
            }

            if (employee.Account == null)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "لا يوجد حساب مرتبط بالموظف");
                return View(model);
            }

            if (deductionType.Account != null && deductionType.Account.CurrencyId != employee.Account.CurrencyId)
            {
                ModelState.AddModelError(nameof(model.DeductionTypeId), "عملة حساب الخصم لا تطابق عملة حساب الموظف");
                return View(model);
            }

            var amount = Math.Round(model.Amount, 2, MidpointRounding.AwayFromZero);
            if (amount <= 0)
            {
                ModelState.AddModelError(nameof(model.Amount), "يجب أن يكون مبلغ الخصم أكبر من صفر");
                return View(model);
            }

            deduction.EmployeeId = employee.Id;
            deduction.DeductionTypeId = deductionType.Id;
            deduction.Amount = amount;
            deduction.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();
            deduction.IsActive = model.IsActive;
            deduction.Year = year;
            deduction.Month = month;
            deduction.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم تحديث خصم الموظف بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var deduction = await _context.EmployeeDeductions
                .FirstOrDefaultAsync(d => d.Id == id);

            if (deduction == null)
            {
                return NotFound();
            }

            _context.EmployeeDeductions.Remove(deduction);
            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم حذف خصم الموظف.";
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
                    Text = e.Branch != null
                        ? $"{e.Name} - {e.Branch.NameAr}"
                        : e.Name
                })
                .ToListAsync();
        }

        private async Task<List<SelectListItem>> GetDeductionTypeSelectListAsync()
        {
            return await _context.DeductionTypes
                .AsNoTracking()
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => new SelectListItem
                {
                    Value = d.Id.ToString(),
                    Text = d.Name
                })
                .ToListAsync();
        }

        private static string ResolveInitialPeriod(int? year, int? month, string? period)
        {
            if (TryParsePeriod(period, out var parsedYear, out var parsedMonth))
            {
                return $"{parsedYear:D4}-{parsedMonth:D2}";
            }

            if (year.HasValue && month.HasValue && year.Value > 0 && month.Value > 0)
            {
                try
                {
                    return new DateTime(year.Value, month.Value, 1).ToString("yyyy-MM");
                }
                catch
                {
                    // Ignore invalid combinations and fall back to today
                }
            }

            return DateTime.Today.ToString("yyyy-MM");
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
