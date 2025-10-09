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

            var culture = new CultureInfo("ar-SA");
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
        public async Task<IActionResult> Employees(int branchId)
        {
            var employeesQuery = _context.Employees
                .AsNoTracking()
                .Include(e => e.Branch)
                .Where(e => e.IsActive);

            if (branchId > 0)
            {
                employeesQuery = employeesQuery.Where(e => e.BranchId == branchId);
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
            if (request == null || request.EmployeeIds == null || !request.EmployeeIds.Any())
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

            var alreadyProcessed = await _context.PayrollBatches
                .AnyAsync(b => b.BranchId == branch.Id && b.Month == request.Month && b.Year == request.Year && b.Status != PayrollBatchStatus.Cancelled);

            if (alreadyProcessed)
            {
                return BadRequest(new { message = "تم تنزيل رواتب هذا الشهر لهذا الفرع مسبقاً" });
            }

            var employees = await _context.Employees
                .Include(e => e.Account)
                .Where(e => request.EmployeeIds.Contains(e.Id) && e.IsActive)
                .ToListAsync();

            if (employees.Count == 0)
            {
                return BadRequest(new { message = "لم يتم العثور على موظفين مطابقين" });
            }

            if (employees.Any(e => e.BranchId != branch.Id))
            {
                return BadRequest(new { message = "يمكن تنزيل رواتب موظفي فرع واحد فقط في كل دفعة" });
            }

            var currencies = employees
                .Select(e => e.Account.CurrencyId)
                .Distinct()
                .ToList();

            if (currencies.Count > 1 || (currencies.Count == 1 && currencies[0] != paymentAccount.CurrencyId))
            {
                return BadRequest(new { message = "يجب أن تكون جميع الحسابات بنفس العملة الخاصة بحساب الرواتب" });
            }

            var total = employees.Sum(e => e.Salary);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var batch = new PayrollBatch
            {
                BranchId = branch.Id,
                PaymentAccountId = paymentAccount.Id,
                Year = request.Year,
                Month = request.Month,
                TotalAmount = total,
                CreatedById = userId,
                Status = PayrollBatchStatus.Draft
            };

            foreach (var employee in employees)
            {
                batch.Lines.Add(new PayrollBatchLine
                {
                    EmployeeId = employee.Id,
                    BranchId = employee.BranchId,
                    Amount = employee.Salary
                });
            }

            _context.PayrollBatches.Add(batch);
            await _context.SaveChangesAsync();

            var summary = new PayrollBatchSummaryViewModel
            {
                BatchId = batch.Id,
                TotalAmount = total,
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
                        TotalAmount = total
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

            var branchLines = batch.Lines.GroupBy(l => l.BranchId);
            foreach (var group in branchLines)
            {
                var branchId = group.Key;
                var batchBranch = await _context.Branches.FindAsync(branchId);
                if (batchBranch == null)
                {
                    return BadRequest(new { message = "تعذر العثور على بيانات الفرع" });
                }

                var lines = new List<JournalEntryLine>();
                foreach (var line in group)
                {
                    lines.Add(new JournalEntryLine
                    {
                        AccountId = line.Employee.AccountId,
                        CreditAmount = line.Amount,
                        DebitAmount = 0,
                        Description = $"راتب {line.Employee.Name}"
                    });
                }

                var groupTotal = group.Sum(l => l.Amount);
                lines.Add(new JournalEntryLine
                {
                    AccountId = batch.PaymentAccountId,
                    CreditAmount = 0,
                    DebitAmount = groupTotal,
                    Description = "صرف رواتب الموظفين"
                });

                var periodDate = new DateTime(
                    batch.Year == 0 ? DateTime.Today.Year : batch.Year,
                    batch.Month == 0 ? DateTime.Today.Month : batch.Month,
                    1);

                var entry = await _journalEntryService.CreateJournalEntryAsync(
                    System.DateTime.Today,
                    $"صرف رواتب الموظفين لشهر {periodDate.ToString("MMMM yyyy", new CultureInfo("ar-SA"))} - {batchBranch.NameAr}",
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

            var summary = new PayrollBatchSummaryViewModel
            {
                BatchId = batch.Id,
                TotalAmount = batch.TotalAmount,
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
                        TotalAmount = batch.TotalAmount
                    }
                }
            };

            var employees = batch.Lines
                .Select(l => new
                {
                    l.EmployeeId,
                    l.Employee.Name,
                    l.Amount,
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

            var processed = await _context.PayrollBatches
                .AsNoTracking()
                .Where(b => b.BranchId == branchId && b.Status != PayrollBatchStatus.Cancelled)
                .Select(b => new { b.Year, b.Month })
                .ToListAsync();

            var processedSet = new HashSet<string>(processed.Select(p => $"{p.Year}-{p.Month}"));
            var culture = new CultureInfo("ar-SA");
            var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var months = Enumerable.Range(0, 12)
                .Select(offset => start.AddMonths(-offset))
                .Select(date => new PayrollMonthOptionViewModel
                {
                    Year = date.Year,
                    Month = date.Month,
                    Name = date.ToString("MMMM yyyy", culture)
                })
                .Where(option => !processedSet.Contains($"{option.Year}-{option.Month}"))
                .OrderByDescending(option => option.Year)
                .ThenByDescending(option => option.Month)
                .ToList();

            return Json(months);
        }
    }
}
