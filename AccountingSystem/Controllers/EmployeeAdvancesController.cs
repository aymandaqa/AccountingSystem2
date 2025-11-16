using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "employeeadvances.view")]
    public class EmployeeAdvancesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IJournalEntryService _journalEntryService;

        public EmployeeAdvancesController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IJournalEntryService journalEntryService)
        {
            _context = context;
            _userManager = userManager;
            _journalEntryService = journalEntryService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var advances = await _context.EmployeeAdvances
                .Where(a => a.CreatedById == user.Id)
                .Include(a => a.Employee).ThenInclude(e => e.Branch)
                .Include(a => a.PaymentAccount)
                .Include(a => a.Currency)
                .OrderByDescending(a => a.Date)
                .ThenByDescending(a => a.Id)
                .ToListAsync();

            return View(advances);
        }

        [Authorize(Policy = "employeeadvances.create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.PaymentAccountId == null || user.PaymentBranchId == null)
            {
                return Challenge();
            }

            var account = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == user.PaymentAccountId.Value);

            if (account == null)
            {
                return Forbid();
            }

            var model = await BuildAdvanceViewModel(new EmployeeAdvanceCreateViewModel
            {
                Date = DateTime.Today
            }, user, account);

            return View(model);
        }

        [HttpPost]
        [Authorize(Policy = "employeeadvances.create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EmployeeAdvanceCreateViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user?.PaymentAccountId == null || user.PaymentBranchId == null)
            {
                return Challenge();
            }

            var account = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == user.PaymentAccountId.Value);

            if (account == null)
            {
                return Forbid();
            }

            var employee = await _context.Employees
                .Include(e => e.Account)
                .FirstOrDefaultAsync(e => e.Id == model.EmployeeId && e.IsActive);

            if (employee == null || employee.BranchId != user.PaymentBranchId)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "الموظف المحدد غير متاح لفرعك");
            }

            if (employee?.Account == null)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "لا يوجد حساب مرتبط بالموظف");
            }
            else if (account.CurrencyId != employee.Account.CurrencyId)
            {
                ModelState.AddModelError(nameof(model.EmployeeId), "عملة حساب الموظف لا تطابق حساب الدفع");
            }

            if (model.Amount <= 0)
            {
                ModelState.AddModelError(nameof(model.Amount), "المبلغ يجب أن يكون أكبر من صفر");
            }

            if (employee != null && model.Amount > employee.Salary)
            {
                ModelState.AddModelError(nameof(model.Amount), "لا يمكن أن يتجاوز المبلغ راتب الموظف");
            }

            if (account.Nature == AccountNature.Debit && model.Amount > account.CurrentBalance)
            {
                ModelState.AddModelError(nameof(model.Amount), "المبلغ يتجاوز رصيد حساب الدفع");
            }

            if (employee?.Account != null)
            {
                var referenceDate = model.Date == default ? DateTime.Today : model.Date;
                var dailyRate = CalculateEmployeeDailyRate(employee.Salary, referenceDate);
                var salaryBalance = CalculateAccruedSalaryBalance(employee, referenceDate, dailyRate);
                var accountAvailable = decimal.Round(CalculateEmployeeAccountAvailableBalance(employee.Account), 2, MidpointRounding.AwayFromZero);
                var maxAdvance = decimal.Round(salaryBalance + accountAvailable, 2, MidpointRounding.AwayFromZero);

                if (maxAdvance <= 0)
                {
                    ModelState.AddModelError(nameof(model.Amount), "لا يوجد رصيد متاح لمنح السلفة حالياً.");
                }
                else if (model.Amount > maxAdvance)
                {
                    ModelState.AddModelError(nameof(model.Amount), $"المبلغ يتجاوز الحد الأقصى المسموح به ({maxAdvance.ToString("N2")} {account.Currency.Code}) بناءً على رصيد السلفة المتاح ورصيد الحساب الحالي.");
                }
            }

            if (!ModelState.IsValid)
            {
                var vm = await BuildAdvanceViewModel(model, user, account);
                return View(vm);
            }

            var advance = new EmployeeAdvance
            {
                EmployeeId = employee!.Id,
                PaymentAccountId = account.Id,
                BranchId = user.PaymentBranchId.Value,
                CurrencyId = account.CurrencyId,
                Amount = model.Amount,
                Date = model.Date == default ? DateTime.Today : model.Date,
                Notes = model.Notes,
                CreatedById = user.Id
            };

            _context.EmployeeAdvances.Add(advance);
            await _context.SaveChangesAsync();

            var description = $"سند صرف سلفة للموظف {employee.Name}";
            if (!string.IsNullOrWhiteSpace(model.Notes))
            {
                description += Environment.NewLine + model.Notes;
            }

            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine { AccountId = employee.AccountId, DebitAmount = model.Amount },
                new JournalEntryLine { AccountId = account.Id, CreditAmount = model.Amount }
            };

            var entry = await _journalEntryService.CreateJournalEntryAsync(
                advance.Date,
                description,
                user.PaymentBranchId.Value,
                user.Id,
                lines,
                JournalEntryStatus.Posted,
                reference: $"EMPADV:{advance.Id}");

            advance.JournalEntryId = entry.Id;
            advance.ReferenceNumber = entry.Number;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Print), new { id = advance.Id });
        }

        public async Task<IActionResult> Print(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var advance = await _context.EmployeeAdvances
                .Include(a => a.Employee).ThenInclude(e => e.Branch)
                .Include(a => a.PaymentAccount).ThenInclude(a => a.Currency)
                .Include(a => a.Currency)
                .Include(a => a.CreatedBy)
                .Include(a => a.Branch)
                .FirstOrDefaultAsync(a => a.Id == id && a.CreatedById == user.Id);

            if (advance == null)
            {
                return NotFound();
            }

            return View(advance);
        }

        private async Task<EmployeeAdvanceCreateViewModel> BuildAdvanceViewModel(
            EmployeeAdvanceCreateViewModel model,
            User user,
            Account account)
        {
            var referenceDate = model.Date == default ? DateTime.Today : model.Date;

            var employees = await _context.Employees
                .Where(e => e.IsActive && e.BranchId == user.PaymentBranchId)
                .Include(e => e.Account)
                .OrderBy(e => e.Name)
                .ToListAsync();

            model.Employees = employees
                .Select(e =>
                {
                    var dailyRate = CalculateEmployeeDailyRate(e.Salary, referenceDate);
                    var salaryBalance = CalculateAccruedSalaryBalance(e, referenceDate, dailyRate);
                    var accountAvailable = decimal.Round(CalculateEmployeeAccountAvailableBalance(e.Account), 2, MidpointRounding.AwayFromZero);
                    var maxAdvance = decimal.Round(salaryBalance + accountAvailable, 2, MidpointRounding.AwayFromZero);

                    return new EmployeeOptionViewModel
                    {
                        Id = e.Id,
                        Name = e.Name,
                        Salary = e.Salary,
                        AccountBalance = e.Account.CurrentBalance,
                        AccountAvailableBalance = accountAvailable,
                        DailySalaryRate = dailyRate,
                        AccruedSalaryBalance = salaryBalance,
                        MaxAdvanceAmount = maxAdvance
                    };
                })
                .ToList();

            model.PaymentAccountName = $"{account.Code} - {account.NameAr}";
            model.PaymentAccountBalance = account.CurrentBalance;
            model.CurrencyCode = account.Currency.Code;

            return model;
        }

        private static decimal CalculateEmployeeDailyRate(decimal salary, DateTime referenceDate)
        {
            var daysInMonth = DateTime.DaysInMonth(referenceDate.Year, referenceDate.Month);
            if (daysInMonth <= 0 || salary <= 0)
            {
                return 0m;
            }

            return decimal.Round(salary / daysInMonth, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal CalculateAccruedSalaryBalance(Employee employee, DateTime referenceDate, decimal dailyRate)
        {
            if (dailyRate <= 0)
            {
                return 0m;
            }

            var effectiveDate = referenceDate.Date;
            var hireDate = employee.HireDate.Date;

            if (effectiveDate < hireDate)
            {
                return 0m;
            }

            var monthStart = new DateTime(effectiveDate.Year, effectiveDate.Month, 1);
            if (hireDate > monthStart)
            {
                monthStart = hireDate;
            }

            var elapsedDays = (effectiveDate - monthStart).Days + 1;
            if (elapsedDays <= 0)
            {
                return 0m;
            }

            var daysInMonth = DateTime.DaysInMonth(effectiveDate.Year, effectiveDate.Month);
            if (elapsedDays > daysInMonth)
            {
                elapsedDays = daysInMonth;
            }

            var accrued = dailyRate * elapsedDays;
            if (accrued > employee.Salary)
            {
                accrued = employee.Salary;
            }

            return decimal.Round(accrued, 2, MidpointRounding.AwayFromZero);
        }

        private static decimal CalculateEmployeeAccountAvailableBalance(Account account)
        {
            var balance = account.CurrentBalance;

            if (balance <= 0)
            {
                return 0m;
            }

            return balance;
        }

    }
}
