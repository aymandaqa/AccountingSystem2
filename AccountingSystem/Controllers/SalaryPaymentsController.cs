using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using AccountingSystem.Services;
using System;
using System.Collections.Generic;
using System.Linq;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Globalization;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "salarypayments.view")]
    public class SalaryPaymentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IJournalEntryService _journalEntryService;

        private async Task<List<int>> GetUserBranchIdsAsync(string userId)
        {
            return await _context.UserBranches
                .Where(ub => ub.UserId == userId)
                .Select(ub => ub.BranchId)
                .ToListAsync();
        }

        public SalaryPaymentsController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IJournalEntryService journalEntryService)
        {
            _context = context;
            _userManager = userManager;
            _journalEntryService = journalEntryService;
        }

        public async Task<IActionResult> Index(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchTerm = null,
            int? branchId = null,
            int page = 1,
            int pageSize = 25)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            page = Math.Max(1, page);
            pageSize = pageSize <= 0 ? 25 : pageSize;

            var userBranchIds = await GetUserBranchIdsAsync(user.Id);

            var paymentsQuery = BuildQuery(user, userBranchIds, branchId, fromDate, toDate);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                paymentsQuery = paymentsQuery.Where(p =>
                    p.Employee.Name.Contains(term) ||
                    p.PaymentAccount.NameAr.Contains(term) ||
                    p.Branch.NameAr.Contains(term) ||
                    (p.ReferenceNumber != null && p.ReferenceNumber.Contains(term)) ||
                    (p.Notes != null && p.Notes.Contains(term)));
            }

            var totalCount = await paymentsQuery.CountAsync();

            var items = await paymentsQuery
                .OrderByDescending(p => p.Date)
                .ThenByDescending(p => p.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .Select(p => new SalaryPaymentListItemViewModel
                {
                    Payment = p,
                    JournalEntryNumber = p.JournalEntry != null ? p.JournalEntry.Number : p.ReferenceNumber,
                    JournalEntryReference = p.JournalEntry != null ? p.JournalEntry.Reference : null
                })
                .ToListAsync();

            var branchesQuery = _context.Branches.AsNoTracking();
            if (userBranchIds.Any())
            {
                branchesQuery = branchesQuery.Where(b => userBranchIds.Contains(b.Id));
            }
            else if (user.PaymentBranchId.HasValue)
            {
                branchesQuery = branchesQuery.Where(b => b.Id == user.PaymentBranchId.Value);
            }

            var branches = await branchesQuery
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(CultureInfo.InvariantCulture),
                    Text = b.NameAr
                })
                .ToListAsync();

            ViewBag.UserBranches = branches;
            ViewBag.SelectedBranchId = branchId;

            var model = new SalaryPaymentIndexViewModel
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = page,
                PageSize = pageSize,
                SearchTerm = searchTerm,
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId
            };

            return View(model);
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

            if (account.Nature == AccountNature.Debit && model.Amount > account.CurrentBalance)
            {
                ModelState.AddModelError(nameof(model.Amount), "المبلغ يتجاوز رصيد حساب الدفع");
            }

            if (employee?.Account != null)
            {
                var employeeAccountBalance = decimal.Round(employee.Account.CurrentBalance, 2, MidpointRounding.AwayFromZero);

                if (employeeAccountBalance <= 0)
                {
                    ModelState.AddModelError(nameof(model.Amount), "لا يوجد رصيد متاح في حساب الموظف.");
                }
                else if (model.Amount > employeeAccountBalance)
                {
                    ModelState.AddModelError(nameof(model.Amount),
                        $"المبلغ يتجاوز رصيد حساب الموظف المتاح ({employeeAccountBalance.ToString("N2")} {account.Currency.Code}).");
                }
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

            var userBranchIds = await GetUserBranchIdsAsync(user.Id);
            var payment = await BuildQuery(user, userBranchIds, null, null, null)
                .FirstOrDefaultAsync(p => p.Id == id);

            if (payment == null)
            {
                return NotFound();
            }

            return View(payment);
        }

        [Authorize(Policy = "salarypayments.view")]
        public async Task<IActionResult> ExportExcel(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? branchId = null,
            string? searchTerm = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var userBranchIds = await GetUserBranchIdsAsync(user.Id);
            var paymentsQuery = BuildQuery(user, userBranchIds, branchId, fromDate, toDate);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var term = searchTerm.Trim();
                paymentsQuery = paymentsQuery.Where(p =>
                    p.Employee.Name.Contains(term) ||
                    p.PaymentAccount.NameAr.Contains(term) ||
                    p.Branch.NameAr.Contains(term) ||
                    (p.ReferenceNumber != null && p.ReferenceNumber.Contains(term)) ||
                    (p.Notes != null && p.Notes.Contains(term)));
            }

            var payments = await paymentsQuery
                .OrderByDescending(p => p.Date)
                .ThenByDescending(p => p.Id)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("SalaryPayments");

            worksheet.Cell(1, 1).Value = "التاريخ";
            worksheet.Cell(1, 2).Value = "الموظف";
            worksheet.Cell(1, 3).Value = "الفرع";
            worksheet.Cell(1, 4).Value = "المبلغ";
            worksheet.Cell(1, 5).Value = "العملة";
            worksheet.Cell(1, 6).Value = "الحساب الدافع";
            worksheet.Cell(1, 7).Value = "رقم القيد";
            worksheet.Cell(1, 8).Value = "ملاحظات";
            worksheet.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var payment in payments)
            {
                worksheet.Cell(row, 1).Value = payment.Date;
                worksheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-MM-dd";
                worksheet.Cell(row, 2).Value = payment.Employee.Name;
                worksheet.Cell(row, 3).Value = payment.Branch.NameAr;
                worksheet.Cell(row, 4).Value = payment.Amount;
                worksheet.Cell(row, 5).Value = payment.Currency.Code;
                worksheet.Cell(row, 6).Value = payment.PaymentAccount.NameAr;
                worksheet.Cell(row, 7).Value = payment.ReferenceNumber ?? payment.JournalEntry?.Number ?? string.Empty;
                worksheet.Cell(row, 8).Value = payment.Notes ?? string.Empty;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"SalaryPayments_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
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

        private IQueryable<SalaryPayment> BuildQuery(
            User user,
            List<int> userBranchIds,
            int? branchId,
            DateTime? fromDate,
            DateTime? toDate)
        {
            var query = _context.SalaryPayments
                .Include(p => p.Employee).ThenInclude(e => e.Branch)
                .Include(p => p.Branch)
                .Include(p => p.PaymentAccount)
                .Include(p => p.Currency)
                .Include(p => p.JournalEntry)
                .Include(p => p.CreatedBy)
                .AsNoTracking();

            if (userBranchIds.Any())
            {
                query = query.Where(p => userBranchIds.Contains(p.BranchId));
            }
            else if (user.PaymentBranchId.HasValue)
            {
                query = query.Where(p => p.BranchId == user.PaymentBranchId.Value);
            }
            else
            {
                query = query.Where(p => p.CreatedById == user.Id);
            }

            if (branchId.HasValue)
            {
                query = query.Where(p => p.BranchId == branchId.Value);
            }

            if (fromDate.HasValue)
            {
                var startDate = fromDate.Value.Date;
                query = query.Where(p => p.Date >= startDate);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1);
                query = query.Where(p => p.Date < endDate);
            }

            return query;
        }
    }
}
