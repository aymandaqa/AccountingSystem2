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

            var paymentAccounts = await _context.Accounts
                .AsNoTracking()
                .Where(a => a.CanPostTransactions)
                .OrderBy(a => a.Code)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}"
                })
                .ToListAsync();

            var history = await _context.PayrollBatches
                .AsNoTracking()
                .Include(b => b.Branch)
                .Include(b => b.PaymentAccount)
                .OrderByDescending(b => b.CreatedAt)
                .Take(20)
                .Select(b => new PayrollBatchHistoryViewModel
                {
                    Id = b.Id,
                    BranchName = b.Branch.NameAr,
                    PaymentAccountName = $"{b.PaymentAccount.Code} - {b.PaymentAccount.NameAr}",
                    TotalAmount = b.TotalAmount,
                    EmployeeCount = b.Lines.Count,
                    Status = b.Status.ToString(),
                    CreatedAt = b.CreatedAt,
                    ConfirmedAt = b.ConfirmedAt,
                    ReferenceNumber = b.ReferenceNumber
                })
                .ToListAsync();

            ViewBag.Branches = branches;
            ViewBag.PaymentAccounts = paymentAccounts;
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

            var branch = await _context.Branches
                .Include(b => b.EmployeeParentAccount)
                .FirstOrDefaultAsync(b => b.Id == request.BranchId);

            if (branch == null)
            {
                return BadRequest(new { message = "الفرع المحدد غير موجود" });
            }

            var paymentAccount = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == request.PaymentAccountId);
            if (paymentAccount == null)
            {
                return BadRequest(new { message = "الحساب المحدد غير موجود" });
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
                return BadRequest(new { message = "يجب أن تكون جميع الحسابات بنفس العملة الخاصة بحساب الدفع" });
            }

            var total = employees.Sum(e => e.Salary);

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty;

            var batch = new PayrollBatch
            {
                BranchId = branch.Id,
                PaymentAccountId = paymentAccount.Id,
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

                var entry = await _journalEntryService.CreateJournalEntryAsync(
                    System.DateTime.Today,
                    $"صرف رواتب الموظفين - {batchBranch.NameAr}",
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

            return Json(new { summary, employees, status = batch.Status.ToString(), reference = batch.ReferenceNumber });
        }
    }
}
