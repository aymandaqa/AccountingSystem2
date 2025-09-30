using System.Collections.Generic;
using System.Linq;
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
                    AccountCode = employee.Account.Code,
                    AccountName = employee.Account.NameAr,
                    IsActive = employee.IsActive
                });
            }

            ViewBag.Branches = branches;
            return View(model);
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

        public class ToggleEmployeeStatusRequest
        {
            public int Id { get; set; }
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
