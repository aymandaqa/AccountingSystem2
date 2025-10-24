using System;
using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Globalization;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "payroll.view")]
    public class PayrollsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IJournalEntryService _journalEntryService;

        public PayrollsController(
            ApplicationDbContext context,
            IJournalEntryService journalEntryService)
        {
            _context = context;
            _journalEntryService = journalEntryService;
        }

        public async Task<IActionResult> Index()
        {
            var branches = await _context.Branches
                .AsNoTracking()
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                })
                .ToListAsync();

            var culture = new CultureInfo("ar");
            var history = await _context.PayrollBatches
                .AsNoTracking()
                .Include(b => b.Branch)
                .OrderByDescending(b => b.CreatedAt)
                .Take(20)
                .Select(b => new PayrollBatchHistoryViewModel
                {
                    Id = b.Id,
                    BranchName = b.Branch.NameAr,
                    PeriodName = new DateTime(b.Year == 0 ? DateTime.Today.Year : b.Year, b.Month == 0 ? 1 : b.Month, 1).ToString("MMMM yyyy", culture),
                    Year = b.Year,
                    Month = b.Month,
                    TotalAmount = b.TotalAmount,
                    EmployeeCount = b.Lines.Count,
                    Status = b.Status.ToString(),
                    CreatedAt = b.CreatedAt,
                    ConfirmedAt = b.ConfirmedAt,
                    ReferenceNumber = b.ReferenceNumber
                })
                .ToListAsync();

            ViewBag.Branches = branches;
            ViewBag.History = history;

            return View();
        }

        [HttpGet]
        public async Task<IActionResult> Employees(int branchId, int? year, int? month)
        {
            var employeesQuery = _context.Employees
                .AsNoTracking()
                .Include(e => e.Branch)
                .Where(e => e.IsActive);

            if (branchId > 0)
            {
                employeesQuery = employeesQuery.Where(e => e.BranchId == branchId);
            }

            if (branchId > 0 && year.HasValue && month.HasValue && year.Value > 0 && month.Value > 0)
            {
                var processedEmployeeIds = await _context.PayrollBatchLines
                    .AsNoTracking()
                    .Where(l => l.BranchId == branchId
                        && l.PayrollBatch.Year == year.Value
                        && l.PayrollBatch.Month == month.Value
                        && l.PayrollBatch.Status != PayrollBatchStatus.Cancelled)
                    .Select(l => l.EmployeeId)
                    .Distinct()
                    .ToListAsync();

                if (processedEmployeeIds.Count > 0)
                {
                    employeesQuery = employeesQuery.Where(e => !processedEmployeeIds.Contains(e.Id));
                }
            }

            var employees = await employeesQuery
                .OrderBy(e => e.Name)
                .Select(e => new PayrollEmployeeViewModel
                {
                    Id = e.Id,
                    Name = e.Name,
                    BranchName = e.Branch.NameAr,
                    BranchId = e.BranchId,
                    Salary = e.Salary,
                    JobTitle = e.JobTitle,
                    IsActive = e.IsActive
                })
                .ToListAsync();

            return Json(employees);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "payroll.process")]
        public async Task<IActionResult> CreateBatch([FromBody] CreatePayrollBatchRequest request)
        {
            if (request == null || request.Employees == null || !request.Employees.Any())
            {
                return BadRequest(new { message = "الرجاء اختيار موظف واحد على الأقل" });
            }

            var requestedEmployees = request.Employees
                .Where(e => e != null && e.EmployeeId > 0)
                .GroupBy(e => e.EmployeeId)
                .Select(g => new PayrollEmployeeSelection
                {
                    EmployeeId = g.Key,
                    DeductionAmount = g.Last().DeductionAmount
                })
                .ToList();

            if (requestedEmployees.Count == 0)
            {
                return BadRequest(new { message = "الرجاء اختيار موظف واحد على الأقل" });
            }

            if (request.Month < 1 || request.Month > 12)
            {
                return BadRequest(new { message = "الشهر المحدد غير صالح" });
            }

            if (request.Year < 2000 || request.Year > 2100)
            {
                return BadRequest(new { message = "السنة المحددة غير صالحة" });
            }

            var employeeIds = requestedEmployees.Select(e => e.EmployeeId).ToList();

            var branch = await _context.Branches
                .Include(b => b.EmployeeParentAccount)
                .FirstOrDefaultAsync(b => b.Id == request.BranchId);

            if (branch == null)
            {
                return BadRequest(new { message = "الفرع المحدد غير موجود" });
            }

            if (!branch.EmployeeParentAccountId.HasValue)
            {
                return BadRequest(new { message = "لم يتم تحديد حساب الرواتب لهذا الفرع" });
            }

            var paymentAccount = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == branch.EmployeeParentAccountId.Value);

            if (paymentAccount == null)
            {
                return BadRequest(new { message = "تعذر العثور على حساب الرواتب المرتبط بالفرع" });
            }

            var payrollExpenseSetting = await _context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "PayrollExpenseAccountId");

            if (payrollExpenseSetting == null || string.IsNullOrWhiteSpace(payrollExpenseSetting.Value) ||
                !int.TryParse(payrollExpenseSetting.Value, out var payrollExpenseAccountId))
            {
                return BadRequest(new { message = "لم يتم ضبط حساب مصروف الرواتب في الإعدادات." });
            }

            var payrollExpenseAccount = await _context.Accounts
                .AsNoTracking()
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == payrollExpenseAccountId);

            if (payrollExpenseAccount == null)
            {
                return BadRequest(new { message = "حساب مصروف الرواتب المحدد غير موجود." });
            }

            var employees = await _context.Employees
                .Include(e => e.Account)
                .Where(e => employeeIds.Contains(e.Id) && e.IsActive)
                .ToListAsync();

            if (employees.Count == 0)
            {
                return BadRequest(new { message = "لم يتم العثور على موظفين مطابقين" });
            }

            if (employees.Any(e => e.BranchId != branch.Id))
            {
                return BadRequest(new { message = "يمكن تنزيل رواتب موظفي فرع واحد فقط في كل دفعة" });
            }

            var processedEmployeeIds = await _context.PayrollBatchLines
                .AsNoTracking()
                .Where(l => employeeIds.Contains(l.EmployeeId)
                    && l.BranchId == branch.Id
                    && l.PayrollBatch.Year == request.Year
                    && l.PayrollBatch.Month == request.Month
                    && l.PayrollBatch.Status != PayrollBatchStatus.Cancelled)
                .Select(l => l.EmployeeId)
                .Distinct()
                .ToListAsync();

            if (processedEmployeeIds.Count > 0)
            {
                var processedNames = employees
                    .Where(e => processedEmployeeIds.Contains(e.Id))
                    .Select(e => e.Name)
                    .ToList();

                var message = processedNames.Count switch
                {
                    0 => "تم تنزيل رواتب بعض الموظفين المحددين لهذا الشهر مسبقاً.",
                    1 => $"تم تنزيل راتب الموظف {processedNames[0]} لهذا الشهر مسبقاً.",
                    _ => $"تم تنزيل رواتب الموظفين التاليين لهذا الشهر مسبقاً: {string.Join(", ", processedNames)}"
                };

                return BadRequest(new { message });
            }

            var currencies = employees
                .Select(e => e.Account.CurrencyId)
                .Distinct()
                .ToList();

            if (currencies.Count > 1 ||
                (currencies.Count == 1 && currencies[0] != paymentAccount.CurrencyId) ||
                (currencies.Count == 1 && currencies[0] != payrollExpenseAccount.CurrencyId))
            {
                return BadRequest(new { message = "يجب أن تكون حسابات الموظفين، والدفع، ومصروف الرواتب بنفس العملة." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var batch = new PayrollBatch
            {
                BranchId = branch.Id,
                PaymentAccountId = paymentAccount.Id,
                Year = request.Year,
                Month = request.Month,
                TotalAmount = 0m,
                CreatedById = userId,
                Status = PayrollBatchStatus.Draft
            };

            var deductionMap = requestedEmployees.ToDictionary(
                e => e.EmployeeId,
                e => e.DeductionAmount < 0 ? 0m : e.DeductionAmount);

            decimal totalGross = 0m;
            decimal totalDeduction = 0m;
            decimal totalNet = 0m;

            foreach (var employee in employees)
            {
                var gross = employee.Salary;
                var deduction = deductionMap.TryGetValue(employee.Id, out var requestedDeduction)
                    ? requestedDeduction
                    : 0m;

                if (deduction < 0)
                {
                    deduction = 0m;
                }

                if (deduction > gross)
                {
                    deduction = gross;
                }

                deduction = Math.Round(deduction, 2, MidpointRounding.AwayFromZero);
                var net = Math.Round(gross - deduction, 2, MidpointRounding.AwayFromZero);

                totalGross += gross;
                totalDeduction += deduction;
                totalNet += net;

                batch.Lines.Add(new PayrollBatchLine
                {
                    EmployeeId = employee.Id,
                    BranchId = employee.BranchId,
                    GrossAmount = gross,
                    DeductionAmount = deduction,
                    Amount = net
                });
            }

            totalGross = Math.Round(totalGross, 2, MidpointRounding.AwayFromZero);
            totalDeduction = Math.Round(totalDeduction, 2, MidpointRounding.AwayFromZero);
            totalNet = Math.Round(totalNet, 2, MidpointRounding.AwayFromZero);

            batch.TotalAmount = totalNet;

            _context.PayrollBatches.Add(batch);
            await _context.SaveChangesAsync();

            var summary = new PayrollBatchSummaryViewModel
            {
                BatchId = batch.Id,
                TotalAmount = totalNet,
                TotalGrossAmount = totalGross,
                TotalDeductionAmount = totalDeduction,
                EmployeeCount = batch.Lines.Count,
                Year = batch.Year,
                Month = batch.Month,
                Branches = new List<PayrollBranchSummaryViewModel>
                {
                    new PayrollBranchSummaryViewModel
                    {
                        BranchId = branch.Id,
                        BranchName = branch.NameAr,
                        EmployeeCount = batch.Lines.Count,
                        TotalAmount = totalNet,
                        TotalGrossAmount = totalGross,
                        TotalDeductionAmount = totalDeduction
                    }
                }
            };

            return Json(new { success = true, summary });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "payroll.process")]
        public async Task<IActionResult> ConfirmBatch([FromBody] ConfirmPayrollBatchRequest request)
        {
            var batch = await _context.PayrollBatches
                .Include(b => b.Branch)
                .Include(b => b.PaymentAccount)
                .Include(b => b.Lines)
                    .ThenInclude(l => l.Employee)
                        .ThenInclude(e => e.Account)
                .FirstOrDefaultAsync(b => b.Id == request.BatchId);

            if (batch == null)
            {
                return NotFound(new { message = "لم يتم العثور على الدفعة" });
            }

            if (batch.Status != PayrollBatchStatus.Draft)
            {
                return BadRequest(new { message = "تمت معالجة هذه الدفعة مسبقاً" });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;
            var journalNumbers = new List<string>();

            var payrollExpenseSetting = await _context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "PayrollExpenseAccountId");

            if (payrollExpenseSetting == null || string.IsNullOrWhiteSpace(payrollExpenseSetting.Value) ||
                !int.TryParse(payrollExpenseSetting.Value, out var payrollExpenseAccountId))
            {
                return BadRequest(new { message = "لم يتم ضبط حساب مصروف الرواتب في الإعدادات." });
            }

            var payrollExpenseAccount = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Code == payrollExpenseAccountId.ToString());

            if (payrollExpenseAccount == null)
            {
                return BadRequest(new { message = "حساب مصروف الرواتب المحدد غير موجود." });
            }

            var branchLines = batch.Lines.GroupBy(l => l.BranchId);
            foreach (var group in branchLines)
            {
                var branchId = group.Key;
                var batchBranch = await _context.Branches.FindAsync(branchId);
                if (batchBranch == null)
                {
                    return BadRequest(new { message = "تعذر العثور على بيانات الفرع" });
                }

                var groupCurrencyIds = group
                    .Select(l => l.Employee.Account.CurrencyId)
                    .Distinct()
                    .ToList();

                if (groupCurrencyIds.Count > 1 || groupCurrencyIds.Any(id => id != payrollExpenseAccount.CurrencyId))
                {
                    return BadRequest(new { message = "عملة حساب مصروف الرواتب لا تطابق عملة حسابات الموظفين." });
                }

                var entryDate = System.DateTime.Today;
                var entryDescription = $"صرف رواتب الموظفين لفرع {batchBranch.NameAr} بتاريخ {entryDate:dd/MM/yyyy}";
                var lines = new List<JournalEntryLine>();
                foreach (var line in group)
                {
                    lines.Add(new JournalEntryLine
                    {
                        AccountId = line.Employee.AccountId,
                        CreditAmount = line.Amount,
                        DebitAmount = 0,
                        Description = $"راتب {line.Employee.Name} عن {entryDate:dd/MM/yyyy}"
                    });
                }

                var groupTotal = group.Sum(l => l.Amount);
                lines.Add(new JournalEntryLine
                {
                    AccountId = payrollExpenseAccount.Id,
                    CreditAmount = 0,
                    DebitAmount = groupTotal,
                    Description = $"مصروف رواتب فرع {batchBranch.NameAr} عن {entryDate:dd/MM/yyyy}"
                });

                var periodDate = new DateTime(
                    batch.Year == 0 ? DateTime.Today.Year : batch.Year,
                    batch.Month == 0 ? DateTime.Today.Month : batch.Month,
                    1);

                var entry = await _journalEntryService.CreateJournalEntryAsync(
                    entryDate,
                    entryDescription,
                    branchId,
                    userId,
                    lines,
                    JournalEntryStatus.Posted,
                    reference: $"PR-{batch.Id}");

                journalNumbers.Add(entry.Number);
            }

            batch.Status = PayrollBatchStatus.Confirmed;
            batch.ConfirmedAt = System.DateTime.Now;
            batch.ConfirmedById = userId;
            batch.ReferenceNumber = string.Join(", ", journalNumbers);

            await _context.SaveChangesAsync();

            return Json(new { success = true, journals = journalNumbers });
        }

        [HttpGet]
        public async Task<IActionResult> BatchDetails(int id)
        {
            var batch = await _context.PayrollBatches
                .AsNoTracking()
                .Include(b => b.Branch)
                .Include(b => b.PaymentAccount)
                .Include(b => b.Lines)
                    .ThenInclude(l => l.Employee)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batch == null)
            {
                return NotFound();
            }

            var totalGross = batch.Lines.Sum(l => l.GrossAmount);
            var totalDeduction = batch.Lines.Sum(l => l.DeductionAmount);
            var totalNet = batch.Lines.Sum(l => l.Amount);

            var summary = new PayrollBatchSummaryViewModel
            {
                BatchId = batch.Id,
                TotalAmount = totalNet,
                TotalGrossAmount = totalGross,
                TotalDeductionAmount = totalDeduction,
                EmployeeCount = batch.Lines.Count,
                Year = batch.Year,
                Month = batch.Month,
                Branches = new List<PayrollBranchSummaryViewModel>
                {
                    new PayrollBranchSummaryViewModel
                    {
                        BranchId = batch.BranchId,
                        BranchName = batch.Branch.NameAr,
                        EmployeeCount = batch.Lines.Count,
                        TotalAmount = totalNet,
                        TotalGrossAmount = totalGross,
                        TotalDeductionAmount = totalDeduction
                    }
                }
            };

            var employees = batch.Lines
                .Select(l => new
                {
                    l.EmployeeId,
                    l.Employee.Name,
                    l.Amount,
                    l.GrossAmount,
                    l.DeductionAmount,
                    l.Employee.JobTitle
                });

            return Json(new
            {
                summary,
                employees,
                status = batch.Status.ToString(),
                reference = batch.ReferenceNumber,
                month = batch.Month,
                year = batch.Year
            });
        }

        [HttpGet]
        public async Task<IActionResult> AvailableMonths(int branchId)
        {
            if (branchId <= 0)
            {
                return Json(Array.Empty<PayrollMonthOptionViewModel>());
            }

            var culture = new CultureInfo("ar");
            var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var months = Enumerable.Range(0, 12)
                .Select(offset => start.AddMonths(-offset))
                .Select(date => new PayrollMonthOptionViewModel
                {
                    Year = date.Year,
                    Month = date.Month,
                    Name = date.ToString("MMMM yyyy", culture)
                })
                .OrderByDescending(option => option.Year)
                .ThenByDescending(option => option.Month)
                .ToList();

            return Json(months);
        }
    }
}
