using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;

namespace AccountingSystem.Controllers
{
    [Authorize]
    public class BranchesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<BranchesController> _logger;

        public BranchesController(ApplicationDbContext context, ILogger<BranchesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var branches = await _context.Branches
                .Include(b => b.UserBranches)
                .Include(b => b.Accounts)
                .OrderBy(b => b.Code)
                .ToListAsync();

            var viewModels = branches.Select(b => new BranchViewModel
            {
                Id = b.Id,
                Code = b.Code,
                NameAr = b.NameAr,
                NameEn = b.NameEn,
                Description = b.Description,
                Address = b.Address,
                Phone = b.Phone,
                Email = b.Email,
                IsActive = b.IsActive,
                CreatedAt = b.CreatedAt,
                UserCount = b.UserBranches.Count,
                AccountCount = b.Accounts.Count
            }).ToList();

            return View(viewModels);
        }

        public async Task<IActionResult> Details(int id)
        {
            var branch = await _context.Branches
                .Include(b => b.UserBranches)
                    .ThenInclude(ub => ub.User)
                .Include(b => b.Accounts)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (branch == null)
            {
                return NotFound();
            }

            var viewModel = new BranchDetailsViewModel
            {
                Id = branch.Id,
                Code = branch.Code,
                NameAr = branch.NameAr,
                NameEn = branch.NameEn,
                Description = branch.Description,
                Address = branch.Address,
                Phone = branch.Phone,
                Email = branch.Email,
                IsActive = branch.IsActive,
                CreatedAt = branch.CreatedAt,
                UpdatedAt = branch.UpdatedAt,
                Users = branch.UserBranches.Select(ub => new BranchUserViewModel
                {
                    UserId = ub.UserId,
                    UserName = ub.User.FullName ?? ub.User.Email ?? string.Empty,
                    IsDefault = ub.IsDefault
                }).ToList(),
                Accounts = branch.Accounts.Select(a => new BranchAccountViewModel
                {
                    AccountId = a.Id,
                    AccountCode = a.Code,
                    AccountName = a.NameAr,
                    AccountType = a.AccountType.ToString()
                }).ToList()
            };

            return View(viewModel);
        }

        public IActionResult Create()
        {
            return View(new CreateBranchViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(CreateBranchViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if code already exists
                if (await _context.Branches.AnyAsync(b => b.Code == model.Code))
                {
                    ModelState.AddModelError("Code", "كود الفرع موجود مسبقاً");
                    return View(model);
                }

                var branch = new Branch
                {
                    Code = model.Code,
                    NameAr = model.NameAr,
                    NameEn = model.NameEn,
                    Description = model.Description,
                    Address = model.Address,
                    Phone = model.Phone,
                    Email = model.Email,
                    IsActive = model.IsActive
                };

                _context.Branches.Add(branch);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Branch {Code} created successfully.", model.Code);
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        public async Task<IActionResult> Edit(int id)
        {
            var branch = await _context.Branches.FindAsync(id);
            if (branch == null)
            {
                return NotFound();
            }

            var viewModel = new EditBranchViewModel
            {
                Id = branch.Id,
                Code = branch.Code,
                NameAr = branch.NameAr,
                NameEn = branch.NameEn,
                Description = branch.Description,
                Address = branch.Address,
                Phone = branch.Phone,
                Email = branch.Email,
                IsActive = branch.IsActive
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(EditBranchViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if code already exists for other branches
                if (await _context.Branches.AnyAsync(b => b.Code == model.Code && b.Id != model.Id))
                {
                    ModelState.AddModelError("Code", "كود الفرع موجود مسبقاً");
                    return View(model);
                }

                var branch = await _context.Branches.FindAsync(model.Id);
                if (branch == null)
                {
                    return NotFound();
                }

                branch.Code = model.Code;
                branch.NameAr = model.NameAr;
                branch.NameEn = model.NameEn;
                branch.Description = model.Description;
                branch.Address = model.Address;
                branch.Phone = model.Phone;
                branch.Email = model.Email;
                branch.IsActive = model.IsActive;
                branch.UpdatedAt = DateTime.UtcNow;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Branch {Code} updated successfully.", model.Code);
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var branch = await _context.Branches
                .Include(b => b.UserBranches)
                .Include(b => b.Accounts)
                .Include(b => b.JournalEntries)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (branch == null)
            {
                return NotFound();
            }

            // Check if branch has related data
            if (branch.UserBranches.Any() || branch.Accounts.Any() || branch.JournalEntries.Any())
            {
                TempData["Error"] = "لا يمكن حذف الفرع لوجود بيانات مرتبطة به";
                return RedirectToAction(nameof(Index));
            }

            _context.Branches.Remove(branch);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Branch {Code} deleted successfully.", branch.Code);
            TempData["Success"] = "تم حذف الفرع بنجاح";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> ManageUsers(int id)
        {
            var branch = await _context.Branches
                .Include(b => b.UserBranches)
                    .ThenInclude(ub => ub.User)
                .FirstOrDefaultAsync(b => b.Id == id);

            if (branch == null)
            {
                return NotFound();
            }

            var allUsers = await _context.Users.Where(u => u.IsActive).ToListAsync();

            var viewModel = new ManageBranchUsersViewModel
            {
                BranchId = branch.Id,
                BranchName = branch.NameAr,
                Users = allUsers.Select(u => new BranchUserAssignmentViewModel
                {
                    UserId = u.Id,
                    UserName = u.FullName ?? u.Email ?? string.Empty,
                    IsAssigned = branch.UserBranches.Any(ub => ub.UserId == u.Id),
                    IsDefault = branch.UserBranches.Any(ub => ub.UserId == u.Id && ub.IsDefault)
                }).ToList()
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ManageUsers(ManageBranchUsersViewModel model)
        {
            var branch = await _context.Branches
                .Include(b => b.UserBranches)
                .FirstOrDefaultAsync(b => b.Id == model.BranchId);

            if (branch == null)
            {
                return NotFound();
            }

            // Remove existing user assignments
            _context.UserBranches.RemoveRange(branch.UserBranches);

            // Add new assignments
            foreach (var user in model.Users.Where(u => u.IsAssigned))
            {
                branch.UserBranches.Add(new UserBranch
                {
                    UserId = user.UserId,
                    BranchId = branch.Id,
                    IsDefault = user.IsDefault
                });
            }

            await _context.SaveChangesAsync();

            _logger.LogInformation("Users updated for branch {Code}.", branch.Code);
            return RedirectToAction(nameof(Details), new { id = model.BranchId });
        }
    }
}

