using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using AccountingSystem.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "salarypayments.view")]
    public class SalaryPaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IJournalEntryService _journalEntryService;

        public SalaryPaymentsController(
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

            var payments = await _context.SalaryPayments
                .Where(p => p.CreatedById == user.Id)
                .Include(p => p.Employee).ThenInclude(e => e.Branch)
                .Include(p => p.Branch)
                .Include(p => p.PaymentAccount)
                .Include(p => p.Currency)
                .OrderByDescending(p => p.Date)
                .ThenByDescending(p => p.Id)
                .ToListAsync();

            return View(payments);
        }

        [Authorize(Policy = "salarypayments.create")]
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

            var model = await BuildSalaryPaymentViewModel(new SalaryPaymentCreateViewModel
            {
                Date = DateTime.Today
            }, user, account);

            return View(model);
        }

        [HttpPost]
        [Authorize(Policy = "salarypayments.create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(SalaryPaymentCreateViewModel model)
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

            if (!ModelState.IsValid)
            {
                var vm = await BuildSalaryPaymentViewModel(model, user, account);
                return View(vm);
            }

            var payment = new SalaryPayment
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

            _context.SalaryPayments.Add(payment);
            await _context.SaveChangesAsync();

            var description = $"سند صرف راتب للموظف {employee.Name}";
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
                payment.Date,
                description,
                user.PaymentBranchId.Value,
                user.Id,
                lines,
                JournalEntryStatus.Posted,
                reference: $"SALPAY:{payment.Id}");

            payment.JournalEntryId = entry.Id;
            payment.ReferenceNumber = entry.Number;
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Print), new { id = payment.Id });
        }

        public async Task<IActionResult> Print(int id)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var payment = await _context.SalaryPayments
                .Include(p => p.Employee).ThenInclude(e => e.Branch)
                .Include(p => p.PaymentAccount).ThenInclude(a => a.Currency)
                .Include(p => p.CreatedBy)
                .Include(p => p.Branch)
                .FirstOrDefaultAsync(p => p.Id == id && p.CreatedById == user.Id);

            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }

        private async Task<SalaryPaymentCreateViewModel> BuildSalaryPaymentViewModel(
            SalaryPaymentCreateViewModel model,
            User user,
            Account account)
        {
            var employees = await _context.Employees
                .Where(e => e.IsActive && e.BranchId == user.PaymentBranchId)
                .Include(e => e.Account)
                .OrderBy(e => e.Name)
                .Select(e => new EmployeeOptionViewModel
                {
                    Id = e.Id,
                    Name = e.Name,
                    Salary = e.Salary,
                    AccountBalance = e.Account.CurrentBalance
                })
                .ToListAsync();

            model.Employees = employees;
            model.PaymentAccountName = $"{account.Code} - {account.NameAr}";
            model.PaymentAccountBalance = account.CurrentBalance;
            model.CurrencyCode = account.Currency.Code;

            return model;
        }
    }
}
