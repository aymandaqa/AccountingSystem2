using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.ModelBinding;
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
        private readonly IJournalEntryService _journalEntryService;
        private readonly IAccountService _accountService;
        private const string EmployeeLoanParentAccountSettingKey = "EmployeeLoansParentAccountId";
        private const string StatusMessageKey = "StatusMessage";

        public EmployeeLoansController(ApplicationDbContext context, UserManager<User> userManager, IJournalEntryService journalEntryService, IAccountService accountService)
        {
            _context = context;
            _userManager = userManager;
            _journalEntryService = journalEntryService;
            _accountService = accountService;
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
                    .Where(i => i.Status == LoanInstallmentStatus.Pending && Math.Round(i.Amount - i.PaidAmount, 2, MidpointRounding.AwayFromZero) > 0)
                    .OrderBy(i => i.DueDate)
                    .ToList();

                var outstanding = pendingInstallments
                    .Sum(i => Math.Round(i.Amount - i.PaidAmount, 2, MidpointRounding.AwayFromZero));

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
                IsActive = true,
                Installments = new List<EmployeeLoanInstallmentViewModel>
                {
                    new EmployeeLoanInstallmentViewModel
                    {
                        DueDate = DateTime.Today,
                        Amount = 0,
                        PaidAmount = 0,
                        Status = LoanInstallmentStatus.Pending
                    }
                }
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

            var loanParentAccount = await GetLoanParentAccountAsync();

            if (loanParentAccount == null)
            {
                ModelState.AddModelError(string.Empty, "لم يتم العثور على حساب قروض الموظفين في الإعدادات أو أنه غير صالح.");
            }

            if (employee?.Account != null && loanParentAccount != null && employee.Account.CurrencyId != loanParentAccount.CurrencyId)
            {
                ModelState.AddModelError(string.Empty, "عملة حساب قروض الموظفين لا تطابق عملة حساب الموظف");
            }

            if (!model.UseCustomSchedule && model.InstallmentCount <= 0)
            {
                ModelState.AddModelError(nameof(model.InstallmentCount), "عدد الأقساط يجب أن يكون أكبر من صفر");
            }

            if (!model.UseCustomSchedule && model.InstallmentAmount <= 0)
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

            List<EmployeeLoanInstallment>? schedule;

            if (model.UseCustomSchedule)
            {
                schedule = BuildCustomInstallmentSchedule(model.Installments, model.PrincipalAmount, model.StartDate, model.Frequency, ModelState);
                if (schedule == null)
                {
                    return View(model);
                }

                if (!ModelState.IsValid)
                {
                    return View(model);
                }
            }
            else
            {
                schedule = BuildInstallmentSchedule(
                    model.PrincipalAmount,
                    model.InstallmentAmount,
                    model.InstallmentCount,
                    model.StartDate,
                    model.Frequency);
            }

            if (!ModelState.IsValid || loanParentAccount == null || employee == null)
            {
                return View(model);
            }

            var accountName = $"قرض {employee.Name}";
            var (loanAccountId, _) = await _accountService.CreateAccountAsync(accountName, loanParentAccount.Id);
            var loanAccount = await _context.Accounts.FirstAsync(a => a.Id == loanAccountId);

            var loan = new EmployeeLoan
            {
                EmployeeId = employee!.Id,
                AccountId = loanAccount.Id,
                PrincipalAmount = Math.Round(model.PrincipalAmount, 2, MidpointRounding.AwayFromZero),
                InstallmentAmount = Math.Round(model.InstallmentAmount, 2, MidpointRounding.AwayFromZero),
                InstallmentCount = schedule.Count,
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

            var description = $"منح قرض للموظف {employee.Name}";
            if (!string.IsNullOrWhiteSpace(model.Notes))
            {
                description += Environment.NewLine + model.Notes;
            }

            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine { AccountId = loanAccount.Id, DebitAmount = model.PrincipalAmount },
                new JournalEntryLine { AccountId = employee.AccountId, CreditAmount = model.PrincipalAmount }
            };

            await _journalEntryService.CreateJournalEntryAsync(
                model.StartDate,
                description,
                employee.BranchId,
                user.Id,
                lines,
                JournalEntryStatus.Posted,
                reference: $"EMPLOAN:{loan.Id}");

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
                .Where(i => i.Status == LoanInstallmentStatus.Pending && GetRemainingAmount(i) > 0)
                .OrderBy(i => i.DueDate)
                .ToList();

            var outstanding = pendingInstallments.Sum(GetRemainingAmount);

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
                Notes = loan.Notes,
                Installments = pendingInstallments
                    .Select(i => new EmployeeLoanInstallmentViewModel
                    {
                        Id = i.Id,
                        Amount = Math.Round(i.Amount - i.PaidAmount, 2, MidpointRounding.AwayFromZero),
                        PaidAmount = 0,
                        DueDate = i.DueDate,
                        Status = i.Status,
                        PaidAt = i.PaidAt
                    })
                    .ToList()
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
                .Where(i => i.Status == LoanInstallmentStatus.Pending && GetRemainingAmount(i) > 0)
                .ToList();

            var outstanding = pendingInstallments.Sum(GetRemainingAmount);
            if (outstanding <= 0)
            {
                TempData[StatusMessageKey] = "لا توجد أقساط متبقية لإعادة الجدولة.";
                return RedirectToAction(nameof(Index));
            }

            if (!model.UseCustomSchedule && model.InstallmentCount <= 0)
            {
                ModelState.AddModelError(nameof(model.InstallmentCount), "عدد الأقساط يجب أن يكون أكبر من صفر");
            }

            if (!model.UseCustomSchedule && model.InstallmentAmount <= 0)
            {
                ModelState.AddModelError(nameof(model.InstallmentAmount), "قيمة القسط يجب أن تكون أكبر من صفر");
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            foreach (var installment in pendingInstallments)
            {
                installment.Status = LoanInstallmentStatus.Rescheduled;
            }

            List<EmployeeLoanInstallment>? schedule;
            if (model.UseCustomSchedule)
            {
                schedule = BuildCustomInstallmentSchedule(model.Installments, outstanding, model.StartDate, model.Frequency, ModelState);
                if (schedule == null)
                {
                    return View(model);
                }
            }
            else
            {
                var newTotal = Math.Round(model.InstallmentAmount * model.InstallmentCount, 2, MidpointRounding.AwayFromZero);
                if (Math.Abs(newTotal - outstanding) > 0.05m)
                {
                    ModelState.AddModelError(nameof(model.InstallmentAmount), "إجمالي جدول السداد الجديد لا يطابق الرصيد المتبقي للقرض.");
                    return View(model);
                }

                schedule = BuildInstallmentSchedule(outstanding, model.InstallmentAmount, model.InstallmentCount, model.StartDate, model.Frequency);
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }
            foreach (var installment in schedule)
            {
                loan.Installments.Add(installment);
            }

            loan.InstallmentAmount = model.UseCustomSchedule
                ? Math.Round(outstanding / Math.Max(schedule.Count, 1), 2, MidpointRounding.AwayFromZero)
                : Math.Round(model.InstallmentAmount, 2, MidpointRounding.AwayFromZero);
            loan.InstallmentCount = loan.Installments.Count(i => i.Status == LoanInstallmentStatus.Pending && Math.Round(i.Amount - i.PaidAmount, 2, MidpointRounding.AwayFromZero) > 0);
            loan.StartDate = model.StartDate.Date;
            loan.EndDate = schedule.LastOrDefault()?.DueDate;
            loan.Frequency = model.Frequency;
            loan.Notes = string.IsNullOrWhiteSpace(model.Notes) ? loan.Notes : model.Notes.Trim();
            loan.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم إعادة جدولة القرض بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Details(int id)
        {
            var loan = await _context.EmployeeLoans
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Branch)
                .Include(l => l.Account)
                .Include(l => l.Installments)
                .Include(l => l.Payments)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loan == null)
            {
                return NotFound();
            }

            ViewBag.StatusMessage = TempData[StatusMessageKey]?.ToString();
            var model = BuildLoanDetailsViewModel(loan);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Pay(int id, [Bind(Prefix = "PaymentForm")] EmployeeLoanPaymentFormViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var loan = await _context.EmployeeLoans
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Branch)
                .Include(l => l.Account)
                .Include(l => l.Installments)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loan == null)
            {
                return NotFound();
            }

            var outstanding = CalculateOutstanding(loan);
            if (outstanding <= 0)
            {
                TempData[StatusMessageKey] = "لا يوجد رصيد متبقٍ للسداد.";
                return RedirectToAction(nameof(Details), new { id });
            }

            if (model.PayFullOutstanding)
            {
                model.Amount = outstanding;
            }

            if (model.Amount <= 0)
            {
                ModelState.AddModelError(nameof(model.Amount), "المبلغ يجب أن يكون أكبر من صفر.");
            }

            if (model.Amount - outstanding > 0.05m)
            {
                ModelState.AddModelError(nameof(model.Amount), "المبلغ المدخل يتجاوز الرصيد المتبقي للقرض.");
            }

            if (!ModelState.IsValid)
            {
                var invalidModel = BuildLoanDetailsViewModel(loan);
                invalidModel.PaymentForm = model;
                return View("Details", invalidModel);
            }

            ApplyPaymentToInstallments(loan, model.Amount, model.PaymentDate);

            var payment = new EmployeeLoanPayment
            {
                EmployeeLoanId = loan.Id,
                Amount = Math.Round(model.Amount, 2, MidpointRounding.AwayFromZero),
                PaymentDate = model.PaymentDate == default ? DateTime.Today : model.PaymentDate,
                Notes = string.IsNullOrWhiteSpace(model.Notes) ? null : model.Notes.Trim(),
                CreatedById = user.Id
            };

            _context.EmployeeLoanPayments.Add(payment);
            await _context.SaveChangesAsync();

            var description = $"سداد قرض للموظف {loan.Employee?.Name ?? loan.EmployeeId.ToString()}";
            if (!string.IsNullOrWhiteSpace(payment.Notes))
            {
                description += Environment.NewLine + payment.Notes;
            }

            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine { AccountId = loan.Employee.AccountId, DebitAmount = payment.Amount },
                new JournalEntryLine { AccountId =  loan.AccountId, CreditAmount = payment.Amount }
            };

            var entry = await _journalEntryService.CreateJournalEntryAsync(
                payment.PaymentDate,
                description,
                loan.Employee.BranchId,
                user.Id,
                lines,
                JournalEntryStatus.Posted,
                reference: $"EMPLOANPAY:{loan.Id}:{payment.Id}");

            payment.JournalEntryId = entry.Id;
            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم تسجيل السداد بنجاح.";
            return RedirectToAction(nameof(Details), new { id });
        }

        public async Task<IActionResult> Print(int id)
        {
            var loan = await _context.EmployeeLoans
                .Include(l => l.Employee)
                    .ThenInclude(e => e.Branch)
                .Include(l => l.Account)
                .Include(l => l.Installments)
                .Include(l => l.Payments)
                .FirstOrDefaultAsync(l => l.Id == id);

            if (loan == null)
            {
                return NotFound();
            }

            var model = BuildLoanDetailsViewModel(loan);
            return View(model);
        }

        private async Task<Account?> GetLoanParentAccountAsync()
        {
            var setting = await _context.SystemSettings
                .FirstOrDefaultAsync(s => s.Key == EmployeeLoanParentAccountSettingKey);

            if (setting == null || !int.TryParse(setting.Value, out var parentAccountId))
            {
                return null;
            }

            return await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == parentAccountId && a.CanHaveChildren);
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

        private static decimal GetRemainingAmount(EmployeeLoanInstallment installment)
        {
            return Math.Max(0, Math.Round(installment.Amount - installment.PaidAmount, 2, MidpointRounding.AwayFromZero));
        }

        private static decimal CalculateOutstanding(EmployeeLoan loan)
        {
            return loan.Installments
                .Where(i => i.Status == LoanInstallmentStatus.Pending)
                .Sum(GetRemainingAmount);
        }

        private static EmployeeLoanDetailsViewModel BuildLoanDetailsViewModel(EmployeeLoan loan)
        {
            var outstanding = CalculateOutstanding(loan);
            var installments = loan.Installments
                .OrderBy(i => i.DueDate)
                .ThenBy(i => i.Id)
                .Select(i => new EmployeeLoanInstallmentViewModel
                {
                    Id = i.Id,
                    Amount = i.Amount,
                    PaidAmount = i.PaidAmount,
                    DueDate = i.DueDate,
                    Status = i.Status,
                    PaidAt = i.PaidAt
                })
                .ToList();

            var paymentForm = new EmployeeLoanPaymentFormViewModel
            {
                Amount = outstanding,
                PaymentDate = DateTime.Today
            };

            return new EmployeeLoanDetailsViewModel
            {
                Id = loan.Id,
                EmployeeName = loan.Employee?.Name ?? string.Empty,
                BranchName = loan.Employee?.Branch?.NameAr ?? string.Empty,
                AccountDisplay = loan.Account != null ? $"{loan.Account.Code} - {loan.Account.NameAr ?? loan.Account.NameEn ?? string.Empty}" : string.Empty,
                PrincipalAmount = loan.PrincipalAmount,
                OutstandingAmount = outstanding,
                InstallmentAmount = loan.InstallmentAmount,
                PendingInstallments = loan.Installments.Count(i => i.Status == LoanInstallmentStatus.Pending && GetRemainingAmount(i) > 0),
                StartDate = loan.StartDate,
                EndDate = loan.EndDate,
                Notes = loan.Notes,
                Frequency = loan.Frequency,
                Installments = installments,
                PaymentForm = paymentForm,
                Payments = loan.Payments
                    .OrderByDescending(p => p.PaymentDate)
                    .Select(p => new EmployeeLoanPaymentHistoryViewModel
                    {
                        Amount = p.Amount,
                        PaymentDate = p.PaymentDate,
                        Notes = p.Notes,
                        JournalEntryId = p.JournalEntryId
                    })
            };
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
                    PaidAmount = 0,
                    Status = LoanInstallmentStatus.Pending
                });

                remaining = Math.Round(remaining - value, 2, MidpointRounding.AwayFromZero);
                dueDate = frequency == LoanInstallmentFrequency.Weekly
                    ? dueDate.AddDays(7)
                    : dueDate.AddMonths(1);
            }

            return installments;
        }

        private static List<EmployeeLoanInstallment>? BuildCustomInstallmentSchedule(
            IEnumerable<EmployeeLoanInstallmentViewModel>? installments,
            decimal totalAmount,
            DateTime startDate,
            LoanInstallmentFrequency frequency,
            ModelStateDictionary modelState)
        {
            var normalized = installments?
                .Where(i => i != null && i.Amount > 0 && i.DueDate != default)
                .OrderBy(i => i.DueDate)
                .ToList() ?? new List<EmployeeLoanInstallmentViewModel>();

            if (!normalized.Any())
            {
                modelState.AddModelError(nameof(EmployeeLoanFormViewModel.Installments), "يجب إضافة قسط واحد على الأقل عند اختيار الجدول المخصص.");
                return null;
            }

            if (normalized.First().DueDate.Date < startDate.Date)
            {
                modelState.AddModelError(nameof(EmployeeLoanFormViewModel.Installments), "تاريخ أول قسط يجب أن يكون بعد تاريخ بداية القرض.");
            }

            var schedule = new List<EmployeeLoanInstallment>();
            decimal sum = 0m;
            foreach (var item in normalized)
            {
                var amount = Math.Round(item.Amount, 2, MidpointRounding.AwayFromZero);
                sum = Math.Round(sum + amount, 2, MidpointRounding.AwayFromZero);
                schedule.Add(new EmployeeLoanInstallment
                {
                    Amount = amount,
                    PaidAmount = Math.Min(Math.Round(item.PaidAmount, 2, MidpointRounding.AwayFromZero), amount),
                    DueDate = item.DueDate.Date,
                    Status = LoanInstallmentStatus.Pending
                });
            }

            if (Math.Abs(sum - Math.Round(totalAmount, 2, MidpointRounding.AwayFromZero)) > 0.05m)
            {
                modelState.AddModelError(nameof(EmployeeLoanFormViewModel.Installments), "إجمالي الأقساط المخصصة لا يساوي قيمة القرض.");
                return null;
            }

            return schedule;
        }

        private static void ApplyPaymentToInstallments(EmployeeLoan loan, decimal paymentAmount, DateTime paymentDate)
        {
            var remainingPayment = Math.Round(paymentAmount, 2, MidpointRounding.AwayFromZero);
            var pendingInstallments = loan.Installments
                .Where(i => i.Status == LoanInstallmentStatus.Pending && GetRemainingAmount(i) > 0)
                .OrderBy(i => i.DueDate)
                .ThenBy(i => i.Id)
                .ToList();

            foreach (var installment in pendingInstallments)
            {
                if (remainingPayment <= 0)
                {
                    break;
                }

                var remainingInstallment = GetRemainingAmount(installment);
                if (remainingInstallment <= 0)
                {
                    continue;
                }

                var applied = Math.Min(remainingInstallment, remainingPayment);
                installment.PaidAmount = Math.Round(installment.PaidAmount + applied, 2, MidpointRounding.AwayFromZero);
                remainingPayment = Math.Round(remainingPayment - applied, 2, MidpointRounding.AwayFromZero);

                if (GetRemainingAmount(installment) <= 0.01m)
                {
                    installment.Status = LoanInstallmentStatus.Paid;
                    installment.PaidAt = paymentDate;
                }
            }

            loan.InstallmentCount = loan.Installments.Count(i => i.Status == LoanInstallmentStatus.Pending && GetRemainingAmount(i) > 0);
            loan.EndDate = loan.Installments
                .Where(i => i.Status == LoanInstallmentStatus.Pending && GetRemainingAmount(i) > 0)
                .OrderByDescending(i => i.DueDate)
                .FirstOrDefault()?.DueDate;
        }
    }
}
