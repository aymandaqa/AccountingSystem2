using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "employees.edit")]
    public class EmployeeLoansController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private const string StatusMessageKey = "StatusMessage";

        public EmployeeLoansController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var loans = await _context.EmployeeLoans
                .AsNoTracking()
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Branch)
                .Include(l => l.Account)
                .Include(l => l.Installments)
                .OrderByDescending(l => l.CreatedAt)
                .ThenByDescending(l => l.Id)
                .ToListAsync();

            var items = loans.Select(l =>
            {
                var pendingInstallments = l.Installments
                    .Where(i => i.Status == LoanInstallmentStatus.Pending)
                    .OrderBy(i => i.DueDate)
                    .ToList();

                var outstanding = pendingInstallments.Sum(i => i.Amount);

                return new EmployeeLoanListItemViewModel
                {
                    Id = l.Id,
                    EmployeeName = l.Employee?.Name ?? string.Empty,
                    BranchName = l.Employee?.Branch?.NameAr ?? string.Empty,
                    AccountDisplay = l.Account != null
                        ? $"{l.Account.Code} - {l.Account.NameAr ?? l.Account.NameEn ?? string.Empty}"
                        : string.Empty,
                    PrincipalAmount = l.PrincipalAmount,
                    InstallmentAmount = l.InstallmentAmount,
                    InstallmentCount = l.InstallmentCount,
                    PendingInstallments = pendingInstallments.Count,
                    OutstandingAmount = outstanding,
                    NextDueDate = pendingInstallments.FirstOrDefault()?.DueDate,
                    Frequency = l.Frequency,
                    IsActive = l.IsActive
                };
            }).ToList();

            ViewBag.StatusMessage = TempData[StatusMessageKey]?.ToString();
            return View(items);
        }

        public async Task<IActionResult> Create()
        {
            var model = new EmployeeLoanFormViewModel
            {
                StartDate = DateTime.Today,
                IsActive = true
            };

            return View(await BuildFormViewModelAsync(model));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeLoanFormViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            model = await BuildFormViewModelAsync(model);

            var employee = await _context.Employees
                .Include(e => e.Account)
                .FirstOrDefaultAsync(e => e.Id == model.EmployeeId && e.IsActive);

            if (employee == null)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "الموظف المحدد غير متاح");
            }

            var account = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == model.AccountId && a.IsActive && a.CanPostTransactions);

            if (account == null)
            {
                ModelState.AddModelError(nameof(model.AccountId), "الحساب المحدد غير متاح");
            }

            if (employee?.Account != null && account != null && employee.Account.CurrencyId != account.CurrencyId)
            {
                ModelState.AddModelError(nameof(model.AccountId), "عملة حساب القرض لا تطابق عملة حساب الموظف");
            }

            if (model.InstallmentCount <= 0)
            {
                ModelState.AddModelError(nameof(model.InstallmentCount), "عدد الأقساط يجب أن يكون أكبر من صفر");
            }

            if (model.InstallmentAmount <= 0)
            {
                ModelState.AddModelError(nameof(model.InstallmentAmount), "قيمة القسط يجب أن تكون أكبر من صفر");
            }

            if (model.PrincipalAmount <= 0)
            {
                ModelState.AddModelError(nameof(model.PrincipalAmount), "قيمة القرض يجب أن تكون أكبر من صفر");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var schedule = BuildInstallmentSchedule(
                model.PrincipalAmount,
                model.InstallmentAmount,
                model.InstallmentCount,
                model.StartDate,
                model.Frequency);

            var loan = new EmployeeLoan
            {
                EmployeeId = employee!.Id,
                AccountId = account!.Id,
                PrincipalAmount = Math.Round(model.PrincipalAmount, 2, MidpointRounding.AwayFromZero),
                InstallmentAmount = Math.Round(model.InstallmentAmount, 2, MidpointRounding.AwayFromZero),
                InstallmentCount = model.InstallmentCount,
                StartDate = model.StartDate.Date,
                EndDate = schedule.LastOrDefault()?.DueDate,
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                Frequency = model.Frequency,
                CreatedById = user.Id,
                IsActive = model.IsActive,
                Installments = schedule
            };

            _context.EmployeeLoans.Add(loan);
            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم إضافة القرض بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var loan = await _context.EmployeeLoans
                .Include(l => l.Employee)
                .Include(l => l.Account)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loan == null)
            {
                return NotFound();
            }

            var model = new EmployeeLoanFormViewModel
            {
                Id = loan.Id,
                EmployeeId = loan.EmployeeId,
                AccountId = loan.AccountId,
                PrincipalAmount = loan.PrincipalAmount,
                InstallmentAmount = loan.InstallmentAmount,
                InstallmentCount = loan.InstallmentCount,
                StartDate = loan.StartDate,
                EndDate = loan.EndDate,
                Frequency = loan.Frequency,
                Notes = loan.Notes,
                IsActive = loan.IsActive
            };

            model = await BuildFormViewModelAsync(model);
            ViewBag.EmployeeName = loan.Employee?.Name;
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EmployeeLoanFormViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            var loan = await _context.EmployeeLoans
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Account)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loan == null)
            {
                return NotFound();
            }

            var account = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == model.AccountId && a.IsActive && a.CanPostTransactions);

            if (account == null)
            {
                ModelState.AddModelError(nameof(model.AccountId), "الحساب المحدد غير متاح");
            }

            if (loan.Employee?.Account != null && account != null && loan.Employee.Account.CurrencyId != account.CurrencyId)
            {
                ModelState.AddModelError(nameof(model.AccountId), "عملة حساب القرض لا تطابق حساب الموظف");
            }

            if (!ModelState.IsValid)
            {
                model = await BuildFormViewModelAsync(model);
                ViewBag.EmployeeName = loan.Employee?.Name;
                return View(model);
            }

            loan.AccountId = account!.Id;
            loan.Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim();
            loan.IsActive = model.IsActive;
            loan.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم تحديث بيانات القرض.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Reschedule(int id)
        {
            var loan = await _context.EmployeeLoans
                .Include(l => l.Employee)
                .Include(l => l.Installments)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loan == null)
            {
                return NotFound();
            }

            var pendingInstallments = loan.Installments
                .Where(i => i.Status == LoanInstallmentStatus.Pending)
                .OrderBy(i => i.DueDate)
                .ToList();

            var outstanding = pendingInstallments.Sum(i => i.Amount);

            if (outstanding <= 0)
            {
                TempData[StatusMessageKey] = "لا توجد أقساط متبقية لإعادة الجدولة.";
                return RedirectToAction(nameof(Index));
            }

            var model = new EmployeeLoanRescheduleViewModel
            {
                Id = loan.Id,
                EmployeeName = loan.Employee?.Name ?? string.Empty,
                OutstandingAmount = outstanding,
                RemainingInstallments = pendingInstallments.Count,
                StartDate = pendingInstallments.FirstOrDefault()?.DueDate ?? DateTime.Today,
                InstallmentAmount = loan.InstallmentAmount,
                InstallmentCount = pendingInstallments.Count,
                Frequency = loan.Frequency,
                CurrentFrequency = loan.Frequency,
                Notes = loan.Notes
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Reschedule(int id, EmployeeLoanRescheduleViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            var loan = await _context.EmployeeLoans
                .Include(l => l.Installments)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loan == null)
            {
                return NotFound();
            }

            var pendingInstallments = loan.Installments
                .Where(i => i.Status == LoanInstallmentStatus.Pending)
                .ToList();

            var outstanding = pendingInstallments.Sum(i => i.Amount);
            if (outstanding <= 0)
            {
                TempData[StatusMessageKey] = "لا توجد أقساط متبقية لإعادة الجدولة.";
                return RedirectToAction(nameof(Index));
            }

            if (model.InstallmentCount <= 0)
            {
                ModelState.AddModelError(nameof(model.InstallmentCount), "عدد الأقساط يجب أن يكون أكبر من صفر");
            }

            if (model.InstallmentAmount <= 0)
            {
                ModelState.AddModelError(nameof(model.InstallmentAmount), "قيمة القسط يجب أن تكون أكبر من صفر");
            }

            var newTotal = Math.Round(model.InstallmentAmount * model.InstallmentCount, 2, MidpointRounding.AwayFromZero);
            if (Math.Abs(newTotal - outstanding) > 0.05m)
            {
                ModelState.AddModelError(nameof(model.InstallmentAmount), "إجمالي جدول السداد الجديد لا يطابق الرصيد المتبقي للقرض.");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            foreach (var installment in pendingInstallments)
            {
                installment.Status = LoanInstallmentStatus.Rescheduled;
            }

            var schedule = BuildInstallmentSchedule(outstanding, model.InstallmentAmount, model.InstallmentCount, model.StartDate, model.Frequency);
            foreach (var installment in schedule)
            {
                loan.Installments.Add(installment);
            }

            loan.InstallmentAmount = Math.Round(model.InstallmentAmount, 2, MidpointRounding.AwayFromZero);
            loan.InstallmentCount = loan.Installments.Count(i => i.Status == LoanInstallmentStatus.Pending);
            loan.StartDate = model.StartDate.Date;
            loan.EndDate = schedule.LastOrDefault()?.DueDate;
            loan.Frequency = model.Frequency;
            loan.Notes = string.IsNullOrWhiteSpace(model.Notes) ? loan.Notes : model.Notes.Trim();
            loan.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم إعادة جدولة القرض بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<EmployeeLoanFormViewModel> BuildFormViewModelAsync(EmployeeLoanFormViewModel model)
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Branch)
                .Where(e => e.IsActive)
                .OrderBy(e => e.Name)
                .Select(e => new SelectListItem
                {
                    Value = e.Id.ToString(),
                    Text = $"{e.Name} - {e.Branch.NameAr}"
                })
                .ToListAsync();

            var employeeCurrencyId = await _context.Employees
                .AsNoTracking()
                .Where(e => e.Id == model.EmployeeId)
                .Select(e => (int?)e.Account.CurrencyId)
                .FirstOrDefaultAsync();

            var accountsQuery = _context.Accounts
                .AsNoTracking()
                .Include(a => a.Currency)
                .Where(a => a.IsActive && a.CanPostTransactions);

            if (employeeCurrencyId.HasValue)
            {
                accountsQuery = accountsQuery.Where(a => a.CurrencyId == employeeCurrencyId.Value);
            }

            var accounts = await accountsQuery
                .OrderBy(a => a.Code)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}" + (a.Currency != null ? $" ({a.Currency.Code})" : string.Empty)
                })
                .ToListAsync();

            model.Employees = employees;
            model.Accounts = accounts;

            return model;
        }

        private static List<EmployeeLoanInstallment> BuildInstallmentSchedule(decimal totalAmount, decimal installmentAmount, int count, DateTime startDate, LoanInstallmentFrequency frequency)
        {
            var installments = new List<EmployeeLoanInstallment>();
            var remaining = Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero);
            var installmentValue = Math.Round(installmentAmount, 2, MidpointRounding.AwayFromZero);
            var dueDate = startDate.Date;

            for (var i = 0; i < count; i++)
            {
                var value = i == count - 1 ? remaining : Math.Min(remaining, installmentValue);
                if (value <= 0)
                {
                    break;
                }

                installments.Add(new EmployeeLoanInstallment
                {
                    Amount = value,
                    DueDate = dueDate,
                    Status = LoanInstallmentStatus.Pending
                });

                remaining = Math.Round(remaining - value, 2, MidpointRounding.AwayFromZero);
                dueDate = frequency == LoanInstallmentFrequency.Weekly
                    ? dueDate.AddDays(7)
                    : dueDate.AddMonths(1);
            }

            return installments;
        }
    }
}
