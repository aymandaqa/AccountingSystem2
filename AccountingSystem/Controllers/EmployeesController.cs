using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using ClosedXML.Excel;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "employees.view")]
    public class EmployeesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAccountService _accountService;

        public EmployeesController(
            ApplicationDbContext context,
            IAccountService accountService)
        {
            _context = context;
            _accountService = accountService;
        }

        public async Task<IActionResult> Index()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Branch)
                .Include(e => e.Account)
                .OrderBy(e => e.Name)
                .ToListAsync();

            var branches = await _context.Branches
                .AsNoTracking()
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                })
                .ToListAsync();

            var model = new List<EmployeeListItemViewModel>();
            foreach (var employee in employees)
            {
                model.Add(new EmployeeListItemViewModel
                {
                    Id = employee.Id,
                    Name = employee.Name,
                    BranchName = employee.Branch.NameAr,
                    BranchId = employee.BranchId,
                    JobTitle = employee.JobTitle,
                    Salary = employee.Salary,
                    AccountCode = employee.Account?.Code ?? string.Empty,
                    AccountName = employee.Account?.NameAr ?? string.Empty,
                    AccountId = employee.AccountId,
                    AccountBalance = employee.Account?.CurrentBalance ?? 0m,
                    IsActive = employee.IsActive,
                    NationalId = employee.NationalId
                });
            }

            ViewBag.Branches = branches;
            return View(model);
        }

        [Authorize(Policy = "employees.view")]
        public async Task<IActionResult> ExportExcel()
        {
            var employees = await _context.Employees
                .AsNoTracking()
                .Include(e => e.Branch)
                .Include(e => e.Account)
                .OrderBy(e => e.Name)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Employees");

            worksheet.Cell(1, 1).Value = "اسم الموظف";
            worksheet.Cell(1, 2).Value = "رقم الهوية";
            worksheet.Cell(1, 3).Value = "المسمى الوظيفي";
            worksheet.Cell(1, 4).Value = "الراتب";
            worksheet.Cell(1, 5).Value = "الفرع (الكود أو الاسم)";
            worksheet.Cell(1, 6).Value = "رقم الهاتف";
            worksheet.Cell(1, 7).Value = "العنوان";
            worksheet.Cell(1, 8).Value = "تاريخ التعيين";
            worksheet.Cell(1, 9).Value = "الحالة";
            worksheet.Cell(1, 10).Value = "كود الحساب";

            worksheet.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var employee in employees)
            {
                worksheet.Cell(row, 1).Value = employee.Name;
                worksheet.Cell(row, 2).Value = employee.NationalId ?? string.Empty;
                worksheet.Cell(row, 3).Value = employee.JobTitle ?? string.Empty;
                worksheet.Cell(row, 4).Value = employee.Salary;
                worksheet.Cell(row, 5).Value = string.IsNullOrWhiteSpace(employee.Branch?.Code)
                    ? employee.Branch?.NameAr ?? string.Empty
                    : $"{employee.Branch.Code} - {employee.Branch.NameAr}";
                worksheet.Cell(row, 6).Value = employee.PhoneNumber ?? string.Empty;
                worksheet.Cell(row, 7).Value = employee.Address ?? string.Empty;
                worksheet.Cell(row, 8).Value = employee.HireDate;
                worksheet.Cell(row, 8).Style.DateFormat.Format = "yyyy-mm-dd";
                worksheet.Cell(row, 9).Value = employee.IsActive ? "نشط" : "موقوف";
                worksheet.Cell(row, 10).Value = employee.Account?.Code ?? string.Empty;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"Employees_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [Authorize(Policy = "employees.create")]
        public async Task<IActionResult> Create()
        {
            var viewModel = new CreateEmployeeViewModel
            {
                Branches = await GetBranchSelectListAsync()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "employees.create")]
        public async Task<IActionResult> Create(CreateEmployeeViewModel model)
        {
            model.Branches = await GetBranchSelectListAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var branch = await _context.Branches
                .Include(b => b.EmployeeParentAccount)
                .FirstOrDefaultAsync(b => b.Id == model.BranchId);

            if (branch == null)
            {
                ModelState.AddModelError(nameof(model.BranchId), "الفرع المحدد غير موجود");
                return View(model);
            }

            if (!branch.EmployeeParentAccountId.HasValue)
            {
                ModelState.AddModelError(nameof(model.BranchId), "الرجاء تحديد الحساب الرئيسي للموظفين للفرع قبل إضافة موظفين");
                return View(model);
            }

            var accountName = $"{branch.NameAr} - {model.Name}";
            var accountResult = await _accountService.CreateAccountAsync(accountName, branch.EmployeeParentAccountId.Value);

            var employee = new Employee
            {
                Name = model.Name,
                Address = model.Address,
                PhoneNumber = model.PhoneNumber,
                NationalId = model.NationalId,
                BranchId = model.BranchId,
                HireDate = model.HireDate,
                Salary = model.Salary,
                JobTitle = model.JobTitle,
                AccountId = accountResult.Id,
                IsActive = model.IsActive
            };

            _context.Employees.Add(employee);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "employees.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var employee = await _context.Employees
                .Include(e => e.Account)
                .FirstOrDefaultAsync(e => e.Id == id);

            if (employee == null)
            {
                return NotFound();
            }

            var viewModel = new EditEmployeeViewModel
            {
                Id = employee.Id,
                Name = employee.Name,
                Address = employee.Address,
                PhoneNumber = employee.PhoneNumber,
                NationalId = employee.NationalId,
                BranchId = employee.BranchId,
                HireDate = employee.HireDate,
                Salary = employee.Salary,
                JobTitle = employee.JobTitle,
                IsActive = employee.IsActive,
                AccountCode = employee.Account.Code,
                Branches = await GetBranchSelectListAsync()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "employees.edit")]
        public async Task<IActionResult> Edit(EditEmployeeViewModel model)
        {
            model.Branches = await GetBranchSelectListAsync();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var employee = await _context.Employees
                .Include(e => e.Account)
                .FirstOrDefaultAsync(e => e.Id == model.Id);

            if (employee == null)
            {
                return NotFound();
            }

            var branch = await _context.Branches
                .Include(b => b.EmployeeParentAccount)
                .FirstOrDefaultAsync(b => b.Id == model.BranchId);

            if (branch == null)
            {
                ModelState.AddModelError(nameof(model.BranchId), "الفرع المحدد غير موجود");
                return View(model);
            }

            if (!branch.EmployeeParentAccountId.HasValue)
            {
                ModelState.AddModelError(nameof(model.BranchId), "الرجاء تحديد الحساب الرئيسي للموظفين للفرع");
                return View(model);
            }

            var account = employee.Account;
            if (account.ParentId != branch.EmployeeParentAccountId)
            {
                var parentAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == branch.EmployeeParentAccountId.Value);

                if (parentAccount == null)
                {
                    ModelState.AddModelError(nameof(model.BranchId), "تعذر العثور على حساب الفرع المحدد");
                    return View(model);
                }

                account.ParentId = parentAccount.Id;
                account.BranchId = parentAccount.BranchId;
                account.Level = parentAccount.Level + 1;
                account.AccountType = parentAccount.AccountType;
                account.Nature = parentAccount.Nature;
                account.Classification = parentAccount.Classification;
                account.SubClassification = parentAccount.SubClassification;
                account.CurrencyId = parentAccount.CurrencyId;
            }

            if (!string.Equals(account.NameAr, model.Name))
            {
                account.NameAr = model.Name;
                account.NameEn = model.Name;
            }

            employee.Name = model.Name;
            employee.Address = model.Address;
            employee.PhoneNumber = model.PhoneNumber;
            employee.NationalId = model.NationalId;
            employee.BranchId = model.BranchId;
            employee.HireDate = model.HireDate;
            employee.Salary = model.Salary;
            employee.JobTitle = model.JobTitle;
            employee.IsActive = model.IsActive;
            employee.UpdatedAt = System.DateTime.Now;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "employees.delete")]
        public async Task<IActionResult> ToggleStatus([FromBody] ToggleEmployeeStatusRequest request)
        {
            if (request == null)
            {
                return Json(new { success = false, message = "طلب غير صالح" });
            }

            var employee = await _context.Employees.FirstOrDefaultAsync(e => e.Id == request.Id);
            if (employee == null)
            {
                return Json(new { success = false, message = "الموظف غير موجود" });
            }

            employee.IsActive = !employee.IsActive;
            employee.UpdatedAt = System.DateTime.Now;
            await _context.SaveChangesAsync();

            return Json(new { success = true, status = employee.IsActive });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "employees.delete")]
        public async Task<IActionResult> Delete([FromBody] DeleteEmployeeRequest request)
        {
            if (request == null)
            {
                return Json(new { success = false, message = "طلب غير صالح" });
            }

            var employee = await _context.Employees
                .FirstOrDefaultAsync(e => e.Id == request.Id);

            if (employee == null)
            {
                return Json(new { success = false, message = "الموظف غير موجود" });
            }

            var hasTransactions = await EmployeeHasTransactionsAsync(employee);
            if (hasTransactions)
            {
                return Json(new { success = false, message = "لا يمكن حذف الموظف لوجود حركات مرتبطة به" });
            }

            var account = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == employee.AccountId);

            _context.Employees.Remove(employee);

            if (account != null)
            {
                _context.Accounts.Remove(account);
            }

            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "employees.create")]
        public async Task<IActionResult> ImportExcel(IFormFile? file)
        {
            if (file == null || file.Length == 0)
            {
                TempData["ImportErrors"] = "يرجى اختيار ملف Excel صالح.";
                return RedirectToAction(nameof(Index));
            }

            if (!string.Equals(Path.GetExtension(file.FileName), ".xlsx", StringComparison.OrdinalIgnoreCase))
            {
                TempData["ImportErrors"] = "يجب أن يكون الملف بامتداد .xlsx";
                return RedirectToAction(nameof(Index));
            }

            var errors = new List<string>();
            var employeesToAdd = new List<Employee>();

            try
            {
                var branches = await _context.Branches
                    .Include(b => b.EmployeeParentAccount)
                    .AsNoTracking()
                    .ToListAsync();

                var existingEmployees = await _context.Employees
                    .Select(e => new { e.BranchId, e.Name })
                    .ToListAsync();
                var existingKeys = new HashSet<string>(existingEmployees
                    .Select(e => $"{e.BranchId}|{e.Name}".ToLowerInvariant()));

                using var stream = new MemoryStream();
                await file.CopyToAsync(stream);
                stream.Position = 0;

                using var workbook = new XLWorkbook(stream);
                var worksheet = workbook.Worksheets.FirstOrDefault();

                if (worksheet == null)
                {
                    errors.Add("تعذر قراءة ورقة العمل من الملف.");
                }
                else
                {
                    var range = worksheet.RangeUsed();
                    if (range == null)
                    {
                        errors.Add("الملف لا يحتوي على بيانات.");
                    }
                    else
                    {
                        foreach (var excelRow in range.RowsUsed().Skip(1))
                        {
                            var usedCells = excelRow.CellsUsed().ToList();
                            if (!usedCells.Any() || usedCells.All(c => string.IsNullOrWhiteSpace(c.GetValue<string>())))
                            {
                                continue;
                            }

                            var rowNumber = excelRow.RowNumber();
                            var name = excelRow.Cell(1).GetValue<string>().Trim();
                            if (string.IsNullOrEmpty(name))
                            {
                                errors.Add($"السطر {rowNumber}: اسم الموظف مطلوب.");
                                continue;
                            }

                            var branchValue = excelRow.Cell(5).GetValue<string>().Trim();
                            if (string.IsNullOrEmpty(branchValue))
                            {
                                errors.Add($"السطر {rowNumber}: الفرع مطلوب.");
                                continue;
                            }

                            var branch = branches.FirstOrDefault(b =>
                                string.Equals(b.Code, branchValue, StringComparison.OrdinalIgnoreCase) ||
                                string.Equals(b.NameAr, branchValue, StringComparison.OrdinalIgnoreCase) ||
                                (!string.IsNullOrWhiteSpace(b.NameEn) && string.Equals(b.NameEn, branchValue, StringComparison.OrdinalIgnoreCase)));

                            if (branch == null)
                            {
                                errors.Add($"السطر {rowNumber}: الفرع \"{branchValue}\" غير موجود.");
                                continue;
                            }

                            if (!branch.EmployeeParentAccountId.HasValue)
                            {
                                errors.Add($"السطر {rowNumber}: الفرع \"{branch.NameAr}\" لا يحتوي على حساب رئيسي للموظفين.");
                                continue;
                            }

                            var key = $"{branch.Id}|{name}".ToLowerInvariant();
                            if (existingKeys.Contains(key))
                            {
                                errors.Add($"السطر {rowNumber}: الموظف \"{name}\" موجود مسبقاً في الفرع المحدد.");
                                continue;
                            }

                            decimal salary = 0;
                            var salaryCell = excelRow.Cell(4);
                            if (!salaryCell.IsEmpty())
                            {
                                if (salaryCell.TryGetValue<decimal>(out var salaryDecimal))
                                {
                                    salary = salaryDecimal;
                                }
                                else
                                {
                                    var salaryText = salaryCell.GetValue<string>();
                                    if (!string.IsNullOrWhiteSpace(salaryText) &&
                                        (decimal.TryParse(salaryText, NumberStyles.Any, CultureInfo.InvariantCulture, out salaryDecimal) ||
                                         decimal.TryParse(salaryText, NumberStyles.Any, CultureInfo.CurrentCulture, out salaryDecimal)))
                                    {
                                        salary = salaryDecimal;
                                    }
                                    else
                                    {
                                        errors.Add($"السطر {rowNumber}: لا يمكن تحويل قيمة الراتب \"{salaryText}\".");
                                        continue;
                                    }
                                }
                            }

                            DateTime hireDate = DateTime.Today;
                            var hireDateCell = excelRow.Cell(8);
                            if (!hireDateCell.IsEmpty())
                            {
                                if (hireDateCell.TryGetValue<DateTime>(out var hireDateValue))
                                {
                                    hireDate = hireDateValue.Date;
                                }
                                else
                                {
                                    var hireDateText = hireDateCell.GetValue<string>();
                                    if (!string.IsNullOrWhiteSpace(hireDateText) &&
                                        (DateTime.TryParse(hireDateText, CultureInfo.CurrentCulture, DateTimeStyles.None, out hireDateValue) ||
                                         DateTime.TryParse(hireDateText, CultureInfo.InvariantCulture, DateTimeStyles.None, out hireDateValue)))
                                    {
                                        hireDate = hireDateValue.Date;
                                    }
                                    else
                                    {
                                        errors.Add($"السطر {rowNumber}: لا يمكن تحويل قيمة تاريخ التعيين \"{hireDateText}\".");
                                        continue;
                                    }
                                }
                            }

                            var statusText = excelRow.Cell(9).GetValue<string>().Trim();
                            var isActive = true;
                            if (!string.IsNullOrEmpty(statusText))
                            {
                                if (bool.TryParse(statusText, out var boolValue))
                                {
                                    isActive = boolValue;
                                }
                                else
                                {
                                    var normalized = statusText.ToLowerInvariant();
                                    if (normalized is "0" or "لا" or "غير نشط" or "موقوف" or "no" or "inactive")
                                    {
                                        isActive = false;
                                    }
                                    else if (normalized is "1" or "نعم" or "نشط" or "نشيط" or "yes" or "active")
                                    {
                                        isActive = true;
                                    }
                                }
                            }

                            var accountResult = await _accountService.CreateAccountAsync($"{branch.NameAr} - {name}", branch.EmployeeParentAccountId.Value);

                            var nationalId = excelRow.Cell(2).GetValue<string>()?.Trim();
                            var jobTitle = excelRow.Cell(3).GetValue<string>()?.Trim();
                            var phone = excelRow.Cell(6).GetValue<string>()?.Trim();
                            var address = excelRow.Cell(7).GetValue<string>()?.Trim();

                            var employee = new Employee
                            {
                                Name = name,
                                JobTitle = string.IsNullOrWhiteSpace(jobTitle) ? null : jobTitle,
                                Salary = salary,
                                BranchId = branch.Id,
                                PhoneNumber = string.IsNullOrWhiteSpace(phone) ? null : phone,
                                NationalId = string.IsNullOrWhiteSpace(nationalId) ? null : nationalId,
                                Address = string.IsNullOrWhiteSpace(address) ? null : address,
                                HireDate = hireDate,
                                AccountId = accountResult.Id,
                                IsActive = isActive
                            };

                            employeesToAdd.Add(employee);
                            existingKeys.Add(key);
                        }
                    }
                }

                if (employeesToAdd.Any())
                {
                    _context.Employees.AddRange(employeesToAdd);
                    await _context.SaveChangesAsync();
                    TempData["ImportSuccess"] = $"تم استيراد {employeesToAdd.Count} موظف بنجاح.";
                }

                if (errors.Any())
                {
                    TempData["ImportErrors"] = string.Join(";;", errors);
                }
                else if (!employeesToAdd.Any())
                {
                    TempData["ImportErrors"] = "لم يتم العثور على بيانات لاستيرادها.";
                }
            }
            catch (Exception ex)
            {
                TempData["ImportErrors"] = $"حدث خطأ أثناء قراءة الملف: {ex.Message}";
            }

            return RedirectToAction(nameof(Index));
        }

        public class ToggleEmployeeStatusRequest
        {
            public int Id { get; set; }
        }

        public class DeleteEmployeeRequest
        {
            public int Id { get; set; }
        }

        private async Task<bool> EmployeeHasTransactionsAsync(Employee employee)
        {
            var hasPayrollLines = await _context.PayrollBatchLines.AnyAsync(l => l.EmployeeId == employee.Id);
            var hasAllowances = await _context.EmployeeAllowances.AnyAsync(a => a.EmployeeId == employee.Id);
            var hasDeductions = await _context.EmployeeDeductions.AnyAsync(d => d.EmployeeId == employee.Id);
            var hasLoans = await _context.EmployeeLoans.AnyAsync(l => l.EmployeeId == employee.Id);
            var hasAdvances = await _context.EmployeeAdvances.AnyAsync(a => a.EmployeeId == employee.Id);
            var hasSalaryPayments = await _context.SalaryPayments.AnyAsync(p => p.EmployeeId == employee.Id);
            var hasJournalEntries = await _context.JournalEntryLines.AnyAsync(l => l.AccountId == employee.AccountId);

            return hasPayrollLines
                || hasAllowances
                || hasDeductions
                || hasLoans
                || hasAdvances
                || hasSalaryPayments
                || hasJournalEntries;
        }

        private async Task<IEnumerable<SelectListItem>> GetBranchSelectListAsync()
        {
            return await _context.Branches
                .AsNoTracking()
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                })
                .ToListAsync();
        }
    }
}
