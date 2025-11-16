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

            var culture = CultureInfo.InvariantCulture;
            var history = await _context.PayrollBatches
                .AsNoTracking()
                .Include(b => b.Branch)
                .OrderByDescending(b => b.CreatedAt)
                .Take(20)
                .Select(b => new PayrollBatchHistoryViewModel
                {
                    Id = b.Id,
                    BranchName = b.Branch.NameAr,
                    PeriodName = new DateTime(b.Year == 0 ? DateTime.Today.Year : b.Year, b.Month == 0 ? 1 : b.Month, 1).ToString("MM/yyyy", culture),
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

            var hasPeriod = year.HasValue && month.HasValue && year.Value > 0 && month.Value > 0;
            var targetYear = hasPeriod ? year!.Value : 0;
            var targetMonth = hasPeriod ? month!.Value : 0;

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
                    IsActive = e.IsActive,
                    Deductions = e.EmployeeDeductions
                        .Where(d => hasPeriod
                            && d.IsActive && d.DeductionType != null && d.DeductionType.IsActive
                            && d.Year == targetYear && d.Month == targetMonth)
                        .Select(d => new PayrollEmployeeDeductionSelection
                        {
                            DeductionTypeId = d.DeductionTypeId,
                            Type = d.DeductionType.Name,
                            Description = d.Description,
                            Amount = d.Amount,
                            AccountName = d.DeductionType.Account != null
                                ? $"{d.DeductionType.Account.Code} - {d.DeductionType.Account.NameAr ?? d.DeductionType.Account.NameEn ?? string.Empty}"
                                : null,
                            AccountCode = d.DeductionType.Account != null ? d.DeductionType.Account.Code : null
                        })
                        .ToList(),
                    Allowances = e.EmployeeAllowances
                        .Where(a => hasPeriod
                            && a.IsActive && a.AllowanceType != null && a.AllowanceType.IsActive
                            && a.Year == targetYear && a.Month == targetMonth)
                        .Select(a => new PayrollEmployeeAllowanceSelection
                        {
                            AllowanceTypeId = a.AllowanceTypeId,
                            Type = a.AllowanceType.Name,
                            Description = a.Description,
                            Amount = a.Amount,
                            AccountName = a.AllowanceType.Account != null
                                ? $"{a.AllowanceType.Account.Code} - {a.AllowanceType.Account.NameAr ?? a.AllowanceType.Account.NameEn ?? string.Empty}"
                                : null,
                            AccountCode = a.AllowanceType.Account != null ? a.AllowanceType.Account.Code : null
                        })
                        .ToList()
                })
                .ToListAsync();

            if (hasPeriod && employees.Count > 0)
            {
                var employeeIds = employees.Select(e => e.Id).ToList();

                var dueInstallments = await _context.EmployeeLoanInstallments
                    .AsNoTracking()
                    .Include(i => i.Loan)
                        .ThenInclude(l => l.Account)
                    .Where(i => i.Status == LoanInstallmentStatus.Pending
                        && i.DueDate.Year == targetYear
                        && i.DueDate.Month == targetMonth
                        && i.Loan.IsActive
                        && employeeIds.Contains(i.Loan.EmployeeId))
                    .ToListAsync();

                var installmentLookup = dueInstallments
                    .GroupBy(i => i.Loan.EmployeeId)
                    .ToDictionary(g => g.Key, g => g.OrderBy(i => i.DueDate).ToList());

                foreach (var employee in employees)
                {
                    if (!installmentLookup.TryGetValue(employee.Id, out var installments))
                    {
                        continue;
                    }

                    foreach (var installment in installments)
                    {
                        var loan = installment.Loan;
                        employee.Deductions.Add(new PayrollEmployeeDeductionSelection
                        {
                            DeductionTypeId = null,
                            Type = $"قسط قرض #{loan.Id}",
                            Description = string.IsNullOrWhiteSpace(loan.Notes)
                                ? $"قسط مستحق بتاريخ {installment.DueDate:dd/MM/yyyy}"
                                : loan.Notes,
                            Amount = installment.Amount,
                            AccountName = loan.Account != null
                                ? $"{loan.Account.Code} - {loan.Account.NameAr ?? loan.Account.NameEn ?? string.Empty}"
                                : null,
                            AccountCode = loan.Account?.Code,
                            AccountId = loan.AccountId,
                            EmployeeLoanInstallmentId = installment.Id
                        });
                    }
                }
            }

            return Json(employees);
        }

        [HttpGet]
        public async Task<IActionResult> DeductionTypes()
        {
            var types = await _context.DeductionTypes
                .AsNoTracking()
                .Include(d => d.Account)
                .Where(d => d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => new
                {
                    d.Id,
                    d.Name,
                    AccountName = d.Account != null
                        ? $"{d.Account.Code} - {d.Account.NameAr ?? d.Account.NameEn ?? string.Empty}"
                        : string.Empty,
                    AccountCode = d.Account != null ? d.Account.Code : null
                })
                .ToListAsync();

            return Json(types);
        }

        [HttpGet]
        public async Task<IActionResult> AllowanceTypes()
        {
            var types = await _context.AllowanceTypes
                .AsNoTracking()
                .Include(a => a.Account)
                .Where(a => a.IsActive)
                .OrderBy(a => a.Name)
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    AccountName = a.Account != null
                        ? $"{a.Account.Code} - {a.Account.NameAr ?? a.Account.NameEn ?? string.Empty}"
                        : string.Empty,
                    AccountCode = a.Account != null ? a.Account.Code : null
                })
                .ToListAsync();

            return Json(types);
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
                .Select(g =>
                {
                    var selection = g.Last();
                    var sanitizedDeductions = (selection.Deductions ?? new List<PayrollEmployeeDeductionSelection>())
                        .Where(d => d != null)
                        .Select(d => new PayrollEmployeeDeductionSelection
                        {
                            DeductionTypeId = d.DeductionTypeId.HasValue && d.DeductionTypeId.Value > 0
                                ? d.DeductionTypeId
                                : null,
                            Amount = SanitizeAmount(d.Amount),
                            Type = TrimAndTruncate(d.Type, 100),
                            Description = TrimAndTruncate(d.Description, 250),
                            AccountId = d.AccountId.HasValue && d.AccountId.Value > 0 ? d.AccountId : null,
                            EmployeeLoanInstallmentId = d.EmployeeLoanInstallmentId.HasValue && d.EmployeeLoanInstallmentId.Value > 0
                                ? d.EmployeeLoanInstallmentId
                                : null
                        })
                        .Where(d => d.Amount > 0)
                        .Take(20)
                        .ToList();

                    var sanitizedAllowances = (selection.Allowances ?? new List<PayrollEmployeeAllowanceSelection>())
                        .Where(a => a != null)
                        .Select(a => new PayrollEmployeeAllowanceSelection
                        {
                            AllowanceTypeId = a.AllowanceTypeId.HasValue && a.AllowanceTypeId.Value > 0
                                ? a.AllowanceTypeId
                                : null,
                            Amount = SanitizeAmount(a.Amount),
                            Type = TrimAndTruncate(a.Type, 100),
                            Description = TrimAndTruncate(a.Description, 250)
                        })
                        .Where(a => a.Amount > 0)
                        .Take(20)
                        .ToList();

                    var hasAllowanceSelection = selection.Allowances != null && sanitizedAllowances.Count > 0;

                    return new PayrollEmployeeSelection
                    {
                        EmployeeId = g.Key,
                        Deductions = sanitizedDeductions,
                        Allowances = sanitizedAllowances,
                        HasAllowanceSelection = hasAllowanceSelection
                    };
                })
                .ToList();

            if (requestedEmployees.Count == 0)
            {
                return BadRequest(new { message = "الرجاء اختيار موظف واحد على الأقل" });
            }

            if (requestedEmployees.SelectMany(e => e.Deductions).Any(d => !d.DeductionTypeId.HasValue && !d.EmployeeLoanInstallmentId.HasValue))
            {
                return BadRequest(new { message = "يجب اختيار نوع خصم أو قسط قرض صالح لكل خصم." });
            }

            if (requestedEmployees.SelectMany(e => e.Deductions)
                .Any(d => d.EmployeeLoanInstallmentId.HasValue && (!d.AccountId.HasValue || d.AccountId.Value <= 0)))
            {
                return BadRequest(new { message = "بعض أقساط القروض تفتقد إلى حساب مرتبط." });
            }

            var requestedTypeIds = requestedEmployees
                .SelectMany(e => e.Deductions)
                .Where(d => d.DeductionTypeId.HasValue)
                .Select(d => d.DeductionTypeId!.Value)
                .Distinct()
                .ToList();

            var deductionTypeMap = await _context.DeductionTypes
                .Include(d => d.Account)
                .Where(d => requestedTypeIds.Contains(d.Id) && d.IsActive)
                .ToDictionaryAsync(d => d.Id);

            if (deductionTypeMap.Count != requestedTypeIds.Count)
            {
                return BadRequest(new { message = "بعض أنواع الخصومات المحددة غير متاحة أو غير نشطة." });
            }

            if (requestedEmployees.SelectMany(e => e.Allowances).Any(a => !a.AllowanceTypeId.HasValue))
            {
                return BadRequest(new { message = "يجب اختيار نوع بدل صالح لكل بدل." });
            }

            var requestedAllowanceTypeIds = requestedEmployees
                .SelectMany(e => e.Allowances)
                .Select(a => a.AllowanceTypeId!.Value)
                .Distinct()
                .ToList();

            var allowanceTypeMap = requestedAllowanceTypeIds.Count > 0
                ? await _context.AllowanceTypes
                    .Include(a => a.Account)
                    .Where(a => requestedAllowanceTypeIds.Contains(a.Id) && a.IsActive)
                    .ToDictionaryAsync(a => a.Id)
                : new Dictionary<int, AllowanceType>();

            if (allowanceTypeMap.Count != requestedAllowanceTypeIds.Count)
            {
                return BadRequest(new { message = "بعض أنواع البدلات المحددة غير متاحة أو غير نشطة." });
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

            var loanInstallments = await _context.EmployeeLoanInstallments
                .Include(i => i.Loan)
                    .ThenInclude(l => l.Account)
                .Where(i => employeeIds.Contains(i.Loan.EmployeeId)
                    && i.Status == LoanInstallmentStatus.Pending
                    && i.Loan.IsActive
                    && i.DueDate.Year == request.Year
                    && i.DueDate.Month == request.Month)
                .ToListAsync();

            var loanInstallmentMap = loanInstallments.ToDictionary(i => i.Id);

            var requestedLoanIds = requestedEmployees
                .SelectMany(e => e.Deductions)
                .Where(d => d.EmployeeLoanInstallmentId.HasValue)
                .Select(d => d.EmployeeLoanInstallmentId!.Value)
                .Distinct()
                .ToList();

            if (requestedLoanIds.Any(id => !loanInstallmentMap.ContainsKey(id)))
            {
                return BadRequest(new { message = "بعض أقساط القروض المحددة غير متاحة لهذه الفترة." });
            }

            var loanInstallmentLookup = loanInstallments
                .GroupBy(i => i.Loan.EmployeeId)
                .ToDictionary(g => g.Key, g => g.OrderBy(i => i.DueDate).ToList());

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

            var selectionMap = requestedEmployees.ToDictionary(
                e => e.EmployeeId,
                e => e);

            var allowanceRecords = await _context.EmployeeAllowances
                .Include(a => a.AllowanceType)
                    .ThenInclude(t => t.Account)
                .Where(a => employeeIds.Contains(a.EmployeeId)
                    && a.IsActive
                    && a.AllowanceType != null
                    && a.AllowanceType.IsActive
                    && a.Year == request.Year
                    && a.Month == request.Month)
                .ToListAsync();

            var allowanceLookup = allowanceRecords
                .GroupBy(a => a.EmployeeId)
                .ToDictionary(g => g.Key, g => g.ToList());

            decimal totalGross = 0m;
            decimal totalDeduction = 0m;
            decimal totalNet = 0m;
            decimal totalAllowance = 0m;
            decimal totalBase = 0m;

            foreach (var employee in employees)
            {
                var baseSalary = Math.Round(employee.Salary, 2, MidpointRounding.AwayFromZero);
                var gross = baseSalary;
                var deductionEntries = new List<PayrollBatchLineDeduction>();
                var allowanceEntries = new List<PayrollBatchLineAllowance>();
                if (employee.Account == null)
                {
                    return BadRequest(new { message = $"لا يوجد حساب مرتبط بالموظف {employee.Name}." });
                }

                selectionMap.TryGetValue(employee.Id, out var employeeSelection);
                var useSelectionAllowances = employeeSelection != null
                    && (employeeSelection.HasAllowanceSelection
                        || (employeeSelection.Allowances?.Count ?? 0) > 0);

                if (useSelectionAllowances && employeeSelection != null)
                {
                    foreach (var allowance in employeeSelection.Allowances)
                    {
                        if (!allowance.AllowanceTypeId.HasValue ||
                            !allowanceTypeMap.TryGetValue(allowance.AllowanceTypeId.Value, out var allowanceType))
                        {
                            return BadRequest(new { message = "أحد أنواع البدلات المحددة غير متاح." });
                        }

                        if (allowanceType.Account == null)
                        {
                            return BadRequest(new { message = $"نوع البدل {allowanceType.Name} لا يحتوي على حساب محدد." });
                        }

                        if (allowanceType.Account.CurrencyId != employee.Account.CurrencyId)
                        {
                            return BadRequest(new { message = $"عملة حساب البدل {allowanceType.Name} لا تطابق عملة حساب الموظف {employee.Name}." });
                        }

                        var amount = Math.Round(allowance.Amount, 2, MidpointRounding.AwayFromZero);
                        if (amount <= 0)
                        {
                            continue;
                        }

                        allowanceEntries.Add(new PayrollBatchLineAllowance
                        {
                            Amount = amount,
                            AllowanceTypeId = allowanceType.Id,
                            AccountId = allowanceType.AccountId,
                            Type = string.IsNullOrWhiteSpace(allowance.Type) ? allowanceType.Name : allowance.Type,
                            Description = string.IsNullOrWhiteSpace(allowance.Description) ? null : allowance.Description
                        });
                    }
                }
                else if (allowanceLookup.TryGetValue(employee.Id, out var employeeAllowances))
                {
                    foreach (var allowance in employeeAllowances)
                    {
                        if (allowance.AllowanceType == null)
                        {
                            return BadRequest(new { message = $"نوع البدل المرتبط بالموظف {employee.Name} غير متاح." });
                        }

                        if (allowance.AllowanceType.Account == null)
                        {
                            return BadRequest(new { message = $"نوع البدل {allowance.AllowanceType.Name} لا يحتوي على حساب محدد." });
                        }

                        if (allowance.AllowanceType.Account.CurrencyId != employee.Account.CurrencyId)
                        {
                            return BadRequest(new { message = $"عملة حساب البدل {allowance.AllowanceType.Name} لا تطابق عملة حساب الموظف {employee.Name}." });
                        }

                        var amount = Math.Round(allowance.Amount, 2, MidpointRounding.AwayFromZero);
                        if (amount <= 0)
                        {
                            continue;
                        }

                        allowanceEntries.Add(new PayrollBatchLineAllowance
                        {
                            Amount = amount,
                            AllowanceTypeId = allowance.AllowanceTypeId,
                            AccountId = allowance.AllowanceType.AccountId,
                            Type = allowance.AllowanceType.Name,
                            Description = string.IsNullOrWhiteSpace(allowance.Description) ? null : allowance.Description
                        });
                    }
                }

                var allowanceTotal = allowanceEntries.Sum(a => a.Amount);
                gross = Math.Round(baseSalary + allowanceTotal, 2, MidpointRounding.AwayFromZero);

                var addedLoanInstallments = new HashSet<int>();
                if (selectionMap.TryGetValue(employee.Id, out employeeSelection))
                {
                    foreach (var deductionItem in employeeSelection.Deductions)
                    {
                        if (deductionItem.EmployeeLoanInstallmentId.HasValue)
                        {
                            if (!loanInstallmentMap.TryGetValue(deductionItem.EmployeeLoanInstallmentId.Value, out var installment)
                                || installment.Loan.EmployeeId != employee.Id)
                            {
                                return BadRequest(new { message = "أحد أقساط القروض المحددة غير مرتبط بالموظف." });
                            }

                            if (installment.Loan.Account == null)
                            {
                                return BadRequest(new { message = "حساب القرض غير محدد بشكل صحيح." });
                            }

                            if (installment.Loan.Account.CurrencyId != employee.Account.CurrencyId)
                            {
                                return BadRequest(new { message = $"عملة حساب القرض لا تطابق عملة حساب الموظف {employee.Name}." });
                            }

                            var loanAmount = Math.Round(installment.Amount, 2, MidpointRounding.AwayFromZero);
                            if (loanAmount <= 0)
                            {
                                continue;
                            }

                            addedLoanInstallments.Add(installment.Id);

                            deductionEntries.Add(new PayrollBatchLineDeduction
                            {
                                Amount = loanAmount,
                                AccountId = installment.Loan.AccountId,
                                EmployeeLoanInstallmentId = installment.Id,
                                Type = string.IsNullOrWhiteSpace(deductionItem.Type) ? $"قسط قرض #{installment.EmployeeLoanId}" : deductionItem.Type,
                                Description = string.IsNullOrWhiteSpace(deductionItem.Description) ? installment.Loan.Notes : deductionItem.Description
                            });

                            continue;
                        }

                        if (!deductionItem.DeductionTypeId.HasValue ||
                            !deductionTypeMap.TryGetValue(deductionItem.DeductionTypeId.Value, out var deductionType))
                        {
                            return BadRequest(new { message = "أحد أنواع الخصومات المحددة غير متاح." });
                        }

                        if (deductionType.Account == null)
                        {
                            return BadRequest(new { message = $"نوع الخصم {deductionType.Name} لا يحتوي على حساب محدد." });
                        }

                        if (deductionType.Account.CurrencyId != employee.Account.CurrencyId)
                        {
                            return BadRequest(new { message = $"عملة حساب الخصم {deductionType.Name} لا تطابق عملة حساب الموظف {employee.Name}." });
                        }

                        var amount = deductionItem.Amount;
                        if (amount <= 0)
                        {
                            continue;
                        }

                        amount = Math.Round(amount, 2, MidpointRounding.AwayFromZero);
                        if (amount <= 0)
                        {
                            continue;
                        }

                        deductionEntries.Add(new PayrollBatchLineDeduction
                        {
                            Amount = amount,
                            DeductionTypeId = deductionType.Id,
                            AccountId = deductionType.AccountId,
                            Type = deductionType.Name,
                            Description = string.IsNullOrWhiteSpace(deductionItem.Description) ? null : deductionItem.Description
                        });
                    }
                }

                if (loanInstallmentLookup.TryGetValue(employee.Id, out var employeeInstallments))
                {
                    foreach (var installment in employeeInstallments)
                    {
                        if (addedLoanInstallments.Contains(installment.Id))
                        {
                            continue;
                        }

                        if (installment.Loan.Account == null)
                        {
                            return BadRequest(new { message = "حساب القرض غير محدد بشكل صحيح." });
                        }

                        if (installment.Loan.Account.CurrencyId != employee.Account.CurrencyId)
                        {
                            return BadRequest(new { message = $"عملة حساب القرض لا تطابق عملة حساب الموظف {employee.Name}." });
                        }

                        var loanAmount = Math.Round(installment.Amount, 2, MidpointRounding.AwayFromZero);
                        if (loanAmount <= 0)
                        {
                            continue;
                        }

                        deductionEntries.Add(new PayrollBatchLineDeduction
                        {
                            Amount = loanAmount,
                            AccountId = installment.Loan.AccountId,
                            EmployeeLoanInstallmentId = installment.Id,
                            Type = $"قسط قرض #{installment.EmployeeLoanId}",
                            Description = installment.Loan.Notes
                        });
                    }
                }

                var deduction = deductionEntries.Sum(d => d.Amount);
                if (deduction > gross)
                {
                    var excess = deduction - gross;
                    var adjustableEntries = deductionEntries.Where(d => !d.EmployeeLoanInstallmentId.HasValue).ToList();
                    var loanEntries = deductionEntries.Where(d => d.EmployeeLoanInstallmentId.HasValue).ToList();

                    void ReduceEntries(List<PayrollBatchLineDeduction> entries)
                    {
                        for (var i = entries.Count - 1; i >= 0 && excess > 0; i--)
                        {
                            var entry = entries[i];
                            if (excess >= entry.Amount)
                            {
                                excess -= entry.Amount;
                                deductionEntries.Remove(entry);
                                continue;
                            }

                            entry.Amount = Math.Round(entry.Amount - excess, 2, MidpointRounding.AwayFromZero);
                            excess = 0;
                            if (entry.Amount <= 0)
                            {
                                deductionEntries.Remove(entry);
                            }
                        }
                    }

                    ReduceEntries(adjustableEntries);
                    if (excess > 0)
                    {
                        ReduceEntries(loanEntries);
                    }

                    deduction = deductionEntries.Sum(d => d.Amount);
                }

                deduction = Math.Clamp(deduction, 0m, gross);
                deduction = Math.Round(deduction, 2, MidpointRounding.AwayFromZero);
                var net = Math.Round(gross - deduction, 2, MidpointRounding.AwayFromZero);
                if (net < 0)
                {
                    net = 0;
                }

                totalBase += baseSalary;
                totalGross += gross;
                totalDeduction += deduction;
                totalNet += net;
                totalAllowance += allowanceTotal;

                batch.Lines.Add(new PayrollBatchLine
                {
                    EmployeeId = employee.Id,
                    BranchId = employee.BranchId,
                    GrossAmount = gross,
                    DeductionAmount = deduction,
                    Amount = net,
                    AllowanceAmount = allowanceTotal,
                    Deductions = deductionEntries,
                    Allowances = allowanceEntries
                });
            }

            totalGross = Math.Round(totalGross, 2, MidpointRounding.AwayFromZero);
            totalDeduction = Math.Round(totalDeduction, 2, MidpointRounding.AwayFromZero);
            totalNet = Math.Round(totalNet, 2, MidpointRounding.AwayFromZero);
            totalAllowance = Math.Round(totalAllowance, 2, MidpointRounding.AwayFromZero);
            totalBase = Math.Round(totalBase, 2, MidpointRounding.AwayFromZero);

            batch.TotalAmount = totalNet;

            _context.PayrollBatches.Add(batch);
            await _context.SaveChangesAsync();

            var summary = new PayrollBatchSummaryViewModel
            {
                BatchId = batch.Id,
                TotalAmount = totalNet,
                TotalGrossAmount = totalGross,
                TotalDeductionAmount = totalDeduction,
                TotalAllowanceAmount = totalAllowance,
                TotalBaseAmount = totalBase,
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
                        TotalDeductionAmount = totalDeduction,
                        TotalAllowanceAmount = totalAllowance,
                        TotalBaseAmount = totalBase
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
                .Include(b => b.Lines)
                    .ThenInclude(l => l.Deductions)
                        .ThenInclude(d => d.DeductionType)
                            .ThenInclude(dt => dt.Account)
                .Include(b => b.Lines)
                    .ThenInclude(l => l.Deductions)
                        .ThenInclude(d => d.Account)
                .Include(b => b.Lines)
                    .ThenInclude(l => l.Allowances)
                        .ThenInclude(a => a.AllowanceType)
                            .ThenInclude(at => at.Account)
                .Include(b => b.Lines)
                    .ThenInclude(l => l.Allowances)
                        .ThenInclude(a => a.Account)
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
                .FirstOrDefaultAsync(a => a.Id == payrollExpenseAccountId);

            if (payrollExpenseAccount == null)
            {
                return BadRequest(new { message = "حساب مصروف الرواتب المحدد غير موجود." });
            }

            var periodDate = new DateTime(
                batch.Year == 0 ? DateTime.Today.Year : batch.Year,
                batch.Month == 0 ? DateTime.Today.Month : batch.Month,
                1);

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
                decimal branchBaseAmount = 0m;
                foreach (var line in group)
                {
                    lines.Add(new JournalEntryLine
                    {
                        AccountId = line.Employee.AccountId,
                        CreditAmount = line.GrossAmount,
                        DebitAmount = 0,
                        Description = $"راتب {line.Employee.Name} عن {entryDate:dd/MM/yyyy}"
                    });

                    var baseAmount = Math.Round(Math.Max(0, line.GrossAmount - line.AllowanceAmount), 2, MidpointRounding.AwayFromZero);
                    branchBaseAmount += baseAmount;

                    foreach (var allowance in line.Allowances)
                    {
                        var accountId = allowance.AccountId ?? allowance.AllowanceType?.AccountId;
                        if (!accountId.HasValue)
                        {
                            continue;
                        }

                        var debitAmount = Math.Round(allowance.Amount, 2, MidpointRounding.AwayFromZero);
                        if (debitAmount <= 0)
                        {
                            continue;
                        }

                        var allowanceName = !string.IsNullOrWhiteSpace(allowance.Description)
                            ? allowance.Description
                            : allowance.AllowanceType?.Name ?? allowance.Type ?? "بدل راتب";

                        var employeeNumber = !string.IsNullOrWhiteSpace(line.Employee.NationalId)
                            ? line.Employee.NationalId!
                            : line.Employee.Id.ToString();

                        var salaryMonthText = periodDate.ToString("MM/yyyy");
                        var description = $"{allowanceName} للموظف {line.Employee.Name} (رقم {employeeNumber}) عن راتب شهر {salaryMonthText}";

                        lines.Add(new JournalEntryLine
                        {
                            AccountId = accountId.Value,
                            DebitAmount = debitAmount,
                            CreditAmount = 0,
                            Description = description
                        });
                    }

                    foreach (var deduction in line.Deductions)
                    {
                        var accountId = deduction.AccountId ?? deduction.DeductionType?.AccountId;
                        if (!accountId.HasValue)
                        {
                            continue;
                        }

                        var creditAmount = Math.Round(deduction.Amount, 2, MidpointRounding.AwayFromZero);
                        if (creditAmount <= 0)
                        {
                            continue;
                        }

                        var deductionName = !string.IsNullOrWhiteSpace(deduction.Description)
                            ? deduction.Description
                            : deduction.DeductionType?.Name ?? deduction.Type ?? "خصم راتب";

                        var employeeNumber = !string.IsNullOrWhiteSpace(line.Employee.NationalId)
                            ? line.Employee.NationalId!
                            : line.Employee.Id.ToString();

                        var salaryMonthText = periodDate.ToString("MM/yyyy");
                        var description = $"{deductionName} للموظف {line.Employee.Name} (رقم {employeeNumber}) عن راتب شهر {salaryMonthText}";

                        lines.Add(new JournalEntryLine
                        {
                            AccountId = line.Employee.AccountId,
                            DebitAmount = creditAmount,
                            CreditAmount = 0,
                            Description = $"خصم {deductionName} من راتب {line.Employee.Name}"
                        });

                        lines.Add(new JournalEntryLine
                        {
                            AccountId = accountId.Value,
                            DebitAmount = 0,
                            CreditAmount = creditAmount,
                            Description = description
                        });
                    }
                }

                var groupBase = Math.Round(branchBaseAmount, 2, MidpointRounding.AwayFromZero);
                lines.Add(new JournalEntryLine
                {
                    AccountId = payrollExpenseAccount.Id,
                    CreditAmount = 0,
                    DebitAmount = groupBase,
                    Description = $"مصروف رواتب فرع {batchBranch.NameAr} عن {entryDate:dd/MM/yyyy}"
                });

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

            var paidInstallmentIds = batch.Lines
                .SelectMany(l => l.Deductions)
                .Where(d => d.EmployeeLoanInstallmentId.HasValue)
                .Select(d => d.EmployeeLoanInstallmentId!.Value)
                .Distinct()
                .ToList();

            if (paidInstallmentIds.Count > 0)
            {
                var paidInstallments = await _context.EmployeeLoanInstallments
                    .Where(i => paidInstallmentIds.Contains(i.Id))
                    .ToListAsync();

                foreach (var installment in paidInstallments)
                {
                    installment.Status = LoanInstallmentStatus.Paid;
                    installment.PaidAt = DateTime.Now;
                    installment.PayrollBatchLineId = batch.Lines
                        .FirstOrDefault(l => l.Deductions.Any(d => d.EmployeeLoanInstallmentId == installment.Id))?.Id;
                }
            }

            batch.Status = PayrollBatchStatus.Confirmed;
            batch.ConfirmedAt = System.DateTime.Now;
            batch.ConfirmedById = userId;
            batch.ReferenceNumber = string.Join(", ", journalNumbers);

            await _context.SaveChangesAsync();

            return Json(new { success = true, journals = journalNumbers });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "payroll.process")]
        public async Task<IActionResult> DeleteBatch([FromBody] ConfirmPayrollBatchRequest request)
        {
            if (request == null || request.BatchId <= 0)
            {
                return BadRequest(new { message = "معرف الدفعة غير صالح" });
            }

            var batch = await _context.PayrollBatches
                .Include(b => b.Lines)
                    .ThenInclude(l => l.Deductions)
                .FirstOrDefaultAsync(b => b.Id == request.BatchId);

            if (batch == null)
            {
                return NotFound(new { message = "لم يتم العثور على الدفعة" });
            }

            if (batch.Status != PayrollBatchStatus.Draft)
            {
                return BadRequest(new { message = "يمكن حذف الدفعات بالحالة مسودة فقط" });
            }

            _context.PayrollBatches.Remove(batch);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
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
                .Include(b => b.Lines)
                    .ThenInclude(l => l.Deductions)
                        .ThenInclude(d => d.DeductionType)
                            .ThenInclude(dt => dt.Account)
                .Include(b => b.Lines)
                    .ThenInclude(l => l.Deductions)
                        .ThenInclude(d => d.Account)
                .Include(b => b.Lines)
                    .ThenInclude(l => l.Allowances)
                        .ThenInclude(a => a.AllowanceType)
                            .ThenInclude(at => at.Account)
                .Include(b => b.Lines)
                    .ThenInclude(l => l.Allowances)
                        .ThenInclude(a => a.Account)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (batch == null)
            {
                return NotFound();
            }

            var totalGross = batch.Lines.Sum(l => l.GrossAmount);
            var totalDeduction = batch.Lines.Sum(l => l.DeductionAmount);
            var totalNet = batch.Lines.Sum(l => l.Amount);
            var totalAllowance = batch.Lines.Sum(l => l.AllowanceAmount);
            var totalBase = totalGross - totalAllowance;

            var summary = new PayrollBatchSummaryViewModel
            {
                BatchId = batch.Id,
                TotalAmount = totalNet,
                TotalGrossAmount = totalGross,
                TotalDeductionAmount = totalDeduction,
                TotalAllowanceAmount = totalAllowance,
                TotalBaseAmount = totalBase,
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
                        TotalDeductionAmount = totalDeduction,
                        TotalAllowanceAmount = totalAllowance,
                        TotalBaseAmount = totalBase
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
                    l.AllowanceAmount,
                    l.Employee.JobTitle,
                    Deductions = l.Deductions
                        .OrderBy(d => d.Id)
                        .Select(d => new
                        {
                            d.Type,
                            d.Description,
                            d.Amount,
                            d.DeductionTypeId,
                            AccountName = d.Account != null
                                ? $"{d.Account.Code} - {d.Account.NameAr ?? d.Account.NameEn ?? string.Empty}"
                                : d.DeductionType != null && d.DeductionType.Account != null
                                    ? $"{d.DeductionType.Account.Code} - {d.DeductionType.Account.NameAr ?? d.DeductionType.Account.NameEn ?? string.Empty}"
                                    : null,
                            AccountCode = d.Account != null
                                ? d.Account.Code
                                : d.DeductionType != null && d.DeductionType.Account != null
                                    ? d.DeductionType.Account.Code
                                    : null
                        })
                        .ToList(),
                    Allowances = l.Allowances
                        .OrderBy(a => a.Id)
                        .Select(a => new
                        {
                            a.Type,
                            a.Description,
                            a.Amount,
                            a.AllowanceTypeId,
                            AccountName = a.Account != null
                                ? $"{a.Account.Code} - {a.Account.NameAr ?? a.Account.NameEn ?? string.Empty}"
                                : a.AllowanceType != null && a.AllowanceType.Account != null
                                    ? $"{a.AllowanceType.Account.Code} - {a.AllowanceType.Account.NameAr ?? a.AllowanceType.Account.NameEn ?? string.Empty}"
                                    : null,
                            AccountCode = a.Account != null
                                ? a.Account.Code
                                : a.AllowanceType != null && a.AllowanceType.Account != null
                                    ? a.AllowanceType.Account.Code
                                    : null
                        })
                        .ToList()
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

        private static decimal SanitizeAmount(decimal amount)
        {
            if (amount < 0)
            {
                return 0m;
            }

            return Math.Round(amount, 2, MidpointRounding.AwayFromZero);
        }

        private static string? TrimAndTruncate(string? value, int maxLength)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return null;
            }

            var trimmed = value.Trim();
            return trimmed.Length <= maxLength ? trimmed : trimmed[..maxLength];
        }

        [HttpGet]
        public async Task<IActionResult> AvailableMonths(int branchId)
        {
            if (branchId <= 0)
            {
                return Json(Array.Empty<PayrollMonthOptionViewModel>());
            }

            var culture = CultureInfo.InvariantCulture;
            var start = new DateTime(DateTime.Today.Year, DateTime.Today.Month, 1);
            var months = Enumerable.Range(0, 12)
                .Select(offset => start.AddMonths(-offset))
                .Select(date => new PayrollMonthOptionViewModel
                {
                    Year = date.Year,
                    Month = date.Month,
                    Name = date.ToString("MM/yyyy", culture)
                })
                .OrderByDescending(option => option.Year)
                .ThenByDescending(option => option.Month)
                .ToList();

            return Json(months);
        }
    }
}
