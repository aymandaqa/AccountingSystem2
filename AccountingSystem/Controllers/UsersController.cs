using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using System;
using System.Collections.Generic;
using System.Linq;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "users.view")]

    public class UsersController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;
        private readonly RoadFnDbContext _roadContext;

        public UsersController(UserManager<User> userManager, ApplicationDbContext context, RoadFnDbContext roadContext)
        {
            _userManager = userManager;
            _context = context;
            _roadContext = roadContext;
        }

        [Authorize(Policy = "users.view")]
        public async Task<IActionResult> Index(string? searchTerm, int page = 1, int pageSize = 20)
        {
            page = Math.Max(page, 1);
            pageSize = pageSize <= 0 ? 20 : Math.Min(pageSize, 100);

            var query = _userManager.Users.AsNoTracking();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                var trimmed = searchTerm.Trim();
                var likeExpression = $"%{trimmed}%";

                query = query.Where(u =>
                    (u.Email != null && EF.Functions.Like(u.Email, likeExpression)) ||
                    EF.Functions.Like((u.FirstName ?? string.Empty) + " " + (u.LastName ?? string.Empty), likeExpression));
            }

            var totalCount = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalCount / (double)pageSize);
            if (totalPages == 0)
            {
                totalPages = 1;
            }

            if (page > totalPages)
            {
                page = totalPages;
            }

            var skip = (page - 1) * pageSize;

            var users = await query
                .OrderBy(u => u.Email)
                .Skip(skip)
                .Take(pageSize)
                .Select(u => new UserListViewModel
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    FullName = (u.FirstName ?? string.Empty) + " " + (u.LastName ?? string.Empty),
                    IsActive = u.IsActive,
                    LastLoginAt = u.LastLoginAt,
                    AgentName = u.Agent != null ? u.Agent.Name : string.Empty
                })
                .ToListAsync();

            var model = new UsersIndexViewModel
            {
                Users = users,
                SearchTerm = searchTerm?.Trim() ?? string.Empty,
                CurrentPage = page,
                PageSize = pageSize,
                TotalPages = totalPages,
                TotalCount = totalCount
            };

            return View(model);
        }

        [Authorize(Policy = "users.create")]
        public async Task<IActionResult> Create()
        {
            var model = new CreateUserViewModel();
            var selectedBranchIds = new HashSet<int>(model.BranchIds ?? new List<int>());
            model.Branches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr,
                    Selected = selectedBranchIds.Contains(b.Id)
                }).ToListAsync();
            model.PaymentBranches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                }).ToListAsync();
            model.DriverBranches = await BuildCompanyBranchSelectListAsync(Enumerable.Empty<int>());
            model.BusinessBranches = await BuildCompanyBranchSelectListAsync(Enumerable.Empty<int>());
            model.CurrencyAccounts = await _context.Currencies
                .Select(c => new UserCurrencyAccountViewModel
                {
                    CurrencyId = c.Id,
                    CurrencyName = c.Name,
                    Accounts = _context.Accounts
                        .Where(a => a.CanPostTransactions && a.CurrencyId == c.Id)
                        .Select(a => new SelectListItem
                        {
                            Value = a.Id.ToString(),
                            Text = $"{a.Code} - {a.NameAr}"
                        }).ToList()
                }).ToListAsync();
            model.Agents = await BuildAgentsSelectListAsync(null);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "users.create")]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
            model.BranchIds ??= new List<int>();
            model.DriverAccountBranchIds ??= new List<int>();
            model.BusinessAccountBranchIds ??= new List<int>();

            if (model.AgentId.HasValue)
            {
                var agentExists = await _context.Agents.AnyAsync(a => a.Id == model.AgentId.Value);
                if (!agentExists)
                {
                    ModelState.AddModelError(nameof(CreateUserViewModel.AgentId), "الوكيل المحدد غير موجود.");
                }
            }

            if (ModelState.IsValid)
            {
                var user = new User
                {
                    UserName = model.Email,
                    Email = model.Email,
                    FirstName = model.FirstName,
                    LastName = model.LastName,
                    IsActive = model.IsActive,
                    PaymentBranchId = model.PaymentBranchId,
                    ExpenseLimit = model.ExpenseLimit,
                    DriverAccountBranchIds = ConvertIdsToString(model.DriverAccountBranchIds),
                    BusinessAccountBranchIds = ConvertIdsToString(model.BusinessAccountBranchIds),
                    AgentId = model.AgentId
                };

                var baseCurrencyId = await _context.Currencies
                    .Where(c => c.IsBase)
                    .Select(c => c.Id)
                    .FirstOrDefaultAsync();
                var baseAccount = model.CurrencyAccounts.FirstOrDefault(ca => ca.CurrencyId == baseCurrencyId);
                if (baseAccount?.AccountId != null)
                {
                    user.PaymentAccountId = baseAccount.AccountId;
                }

                var result = await _userManager.CreateAsync(user, model.Password);
                if (result.Succeeded)
                {
                    foreach (var branchId in model.BranchIds)
                    {
                        _context.UserBranches.Add(new UserBranch
                        {
                            UserId = user.Id,
                            BranchId = branchId,
                            IsDefault = branchId == model.BranchIds.FirstOrDefault()
                        });
                    }
                    foreach (var ca in model.CurrencyAccounts)
                    {
                        if (ca.AccountId.HasValue)
                        {
                            _context.UserPaymentAccounts.Add(new UserPaymentAccount
                            {
                                UserId = user.Id,
                                CurrencyId = ca.CurrencyId,
                                AccountId = ca.AccountId.Value
                            });
                        }
                    }
                    await _context.SaveChangesAsync();
                    return RedirectToAction(nameof(Index));
                }

                foreach (var error in result.Errors)
                    ModelState.AddModelError(string.Empty, error.Description);
            }
            var selectedBranchIds = new HashSet<int>(model.BranchIds ?? new List<int>());
            model.Branches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr,
                    Selected = selectedBranchIds.Contains(b.Id)
                }).ToListAsync();
            model.PaymentBranches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                }).ToListAsync();
            model.DriverBranches = await BuildCompanyBranchSelectListAsync(model.DriverAccountBranchIds ?? new List<int>());
            model.BusinessBranches = await BuildCompanyBranchSelectListAsync(model.BusinessAccountBranchIds ?? new List<int>());
            model.CurrencyAccounts = await _context.Currencies
                .Select(c => new UserCurrencyAccountViewModel
                {
                    CurrencyId = c.Id,
                    CurrencyName = c.Name,
                    Accounts = _context.Accounts
                        .Where(a => a.CanPostTransactions && a.CurrencyId == c.Id)
                        .Select(a => new SelectListItem
                        {
                            Value = a.Id.ToString(),
                            Text = $"{a.Code} - {a.NameAr}"
                        }).ToList()
                }).ToListAsync();
            model.Agents = await BuildAgentsSelectListAsync(model.AgentId);
            return View(model);
        }

        [Authorize(Policy = "users.edit")]
        public async Task<IActionResult> Edit(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var model = new EditUserViewModel
            {
                Id = user.Id,
                Email = user.Email ?? string.Empty,
                FirstName = user.FirstName,
                LastName = user.LastName,
                IsActive = user.IsActive,
                BranchIds = await _context.UserBranches
                    .Where(ub => ub.UserId == id)
                    .Select(ub => ub.BranchId)
                    .ToListAsync(),
                PaymentBranchId = user.PaymentBranchId,
                ExpenseLimit = user.ExpenseLimit,
                DriverAccountBranchIds = ParseIds(user.DriverAccountBranchIds),
                BusinessAccountBranchIds = ParseIds(user.BusinessAccountBranchIds),
                AgentId = user.AgentId
            };

            model.Branches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr,
                    Selected = model.BranchIds.Contains(b.Id)
                }).ToListAsync();
            model.PaymentBranches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr,
                    Selected = b.Id == model.PaymentBranchId
                }).ToListAsync();
            model.DriverBranches = await BuildCompanyBranchSelectListAsync(model.DriverAccountBranchIds);
            model.BusinessBranches = await BuildCompanyBranchSelectListAsync(model.BusinessAccountBranchIds);
            model.Agents = await BuildAgentsSelectListAsync(model.AgentId);
            var userAccounts = await _context.UserPaymentAccounts
                .Where(upa => upa.UserId == id)
                .ToListAsync();
            var currencies = await _context.Currencies.ToListAsync();
            model.CurrencyAccounts = new List<UserCurrencyAccountViewModel>();
            foreach (var currency in currencies)
            {
                var accountList = await _context.Accounts
                    .Where(a => a.CanPostTransactions && a.CurrencyId == currency.Id)
                    .ToListAsync();

                var accounts = accountList.Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}",
                    Selected = userAccounts.Any(ua => ua.CurrencyId == currency.Id && ua.AccountId == a.Id)
                }).ToList();
                model.CurrencyAccounts.Add(new UserCurrencyAccountViewModel
                {
                    CurrencyId = currency.Id,
                    CurrencyName = currency.Name,
                    AccountId = userAccounts.FirstOrDefault(ua => ua.CurrencyId == currency.Id)?.AccountId,
                    Accounts = accounts
                });
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "users.edit")]
        public async Task<IActionResult> Edit(EditUserViewModel model)
        {
            model.BranchIds ??= new List<int>();
            model.DriverAccountBranchIds ??= new List<int>();
            model.BusinessAccountBranchIds ??= new List<int>();

            if (model.AgentId.HasValue)
            {
                var agentExists = await _context.Agents.AnyAsync(a => a.Id == model.AgentId.Value);
                if (!agentExists)
                {
                    ModelState.AddModelError(nameof(EditUserViewModel.AgentId), "الوكيل المحدد غير موجود.");
                }
            }

            if (!ModelState.IsValid)
            {
                await PopulateEditSelectionsAsync(model);
                return View(model);
            }

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            user.Email = model.Email;
            user.UserName = model.Email;
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.IsActive = model.IsActive;
            user.PaymentBranchId = model.PaymentBranchId;
            user.ExpenseLimit = model.ExpenseLimit;
            user.DriverAccountBranchIds = ConvertIdsToString(model.DriverAccountBranchIds);
            user.BusinessAccountBranchIds = ConvertIdsToString(model.BusinessAccountBranchIds);
            user.AgentId = model.AgentId;

            var baseCurrencyIdEdit = await _context.Currencies
                .Where(c => c.IsBase)
                .Select(c => c.Id)
                .FirstOrDefaultAsync();
            var baseAccountEdit = model.CurrencyAccounts.FirstOrDefault(ca => ca.CurrencyId == baseCurrencyIdEdit);
            user.PaymentAccountId = baseAccountEdit?.AccountId;

            var result = await _userManager.UpdateAsync(user);
            if (result.Succeeded)
            {
                var existing = await _context.UserBranches
                    .Where(ub => ub.UserId == user.Id)
                    .ToListAsync();
                _context.UserBranches.RemoveRange(existing);

                foreach (var branchId in model.BranchIds)
                {
                    _context.UserBranches.Add(new UserBranch
                    {
                        UserId = user.Id,
                        BranchId = branchId,
                        IsDefault = branchId == model.BranchIds.FirstOrDefault()
                    });
                }
                var existingAccounts = await _context.UserPaymentAccounts
                    .Where(upa => upa.UserId == user.Id)
                    .ToListAsync();
                _context.UserPaymentAccounts.RemoveRange(existingAccounts);
                foreach (var ca in model.CurrencyAccounts)
                {
                    if (ca.AccountId.HasValue)
                    {
                        _context.UserPaymentAccounts.Add(new UserPaymentAccount
                        {
                            UserId = user.Id,
                            CurrencyId = ca.CurrencyId,
                            AccountId = ca.AccountId.Value
                        });
                    }
                }
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            foreach (var error in result.Errors)
                ModelState.AddModelError(string.Empty, error.Description);

            await PopulateEditSelectionsAsync(model);
            return View(model);
        }

        private static List<int> ParseIds(string? value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return new List<int>();
            }

            return value
                .Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(token =>
                {
                    var trimmed = token.Trim();
                    return int.TryParse(trimmed, out var parsed) ? (int?)parsed : null;
                })
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();
        }

        private static string? ConvertIdsToString(IEnumerable<int>? ids)
        {
            if (ids == null)
            {
                return null;
            }

            var normalized = ids
                .Where(id => id > 0)
                .Distinct()
                .ToList();

            return normalized.Count == 0 ? null : string.Join(",", normalized);
        }

        private async Task PopulateEditSelectionsAsync(EditUserViewModel model)
        {
            var selectedBranchIds = new HashSet<int>(model.BranchIds ?? new List<int>());

            model.Branches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr,
                    Selected = selectedBranchIds.Contains(b.Id)
                }).ToListAsync();

            model.PaymentBranches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr,
                    Selected = b.Id == model.PaymentBranchId
                }).ToListAsync();

            model.DriverBranches = await BuildCompanyBranchSelectListAsync(model.DriverAccountBranchIds ?? new List<int>());
            model.BusinessBranches = await BuildCompanyBranchSelectListAsync(model.BusinessAccountBranchIds ?? new List<int>());
            model.Agents = await BuildAgentsSelectListAsync(model.AgentId);

            var userAccounts = await _context.UserPaymentAccounts
                .Where(upa => upa.UserId == model.Id)
                .ToListAsync();

            var currencies = await _context.Currencies.ToListAsync();
            model.CurrencyAccounts = new List<UserCurrencyAccountViewModel>();

            foreach (var currency in currencies)
            {
                var accountList = await _context.Accounts
                    .Where(a => a.CanPostTransactions && a.CurrencyId == currency.Id)
                    .ToListAsync();

                var accounts = accountList.Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}",
                    Selected = userAccounts.Any(ua => ua.CurrencyId == currency.Id && ua.AccountId == a.Id)
                }).ToList();

                model.CurrencyAccounts.Add(new UserCurrencyAccountViewModel
                {
                    CurrencyId = currency.Id,
                    CurrencyName = currency.Name,
                    AccountId = userAccounts.FirstOrDefault(ua => ua.CurrencyId == currency.Id)?.AccountId,
                    Accounts = accounts
                });
            }
        }

        private async Task<List<SelectListItem>> BuildCompanyBranchSelectListAsync(IEnumerable<int>? selectedIds)
        {
            var selectedSet = new HashSet<int>(selectedIds ?? Enumerable.Empty<int>());

            return await _roadContext.CompanyBranches
                .OrderBy(b => b.BranchName)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.BranchName ?? string.Empty,
                    Selected = selectedSet.Contains(b.Id)
                })
                .ToListAsync();
        }

        private async Task<List<SelectListItem>> BuildAgentsSelectListAsync(int? selectedId)
        {
            var agents = await _context.Agents
                .OrderBy(a => a.Name)
                .Select(a => new
                {
                    a.Id,
                    a.Name,
                    BranchName = a.Branch != null ? a.Branch.NameAr : string.Empty
                })
                .ToListAsync();

            return agents
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = string.IsNullOrEmpty(a.BranchName) ? a.Name : $"{a.Name} - {a.BranchName}",
                    Selected = selectedId.HasValue && selectedId.Value == a.Id
                })
                .ToList();
        }

        [Authorize(Policy = "users.edit")]
        public async Task<IActionResult> ManagePermissions(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();

            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var permissions = await _context.Permissions
                .OrderBy(p => p.Category)
                .ThenBy(p => p.DisplayName)
                .ToListAsync();

            var userPermissions = await _context.UserPermissions
                .Where(up => up.UserId == id && up.IsGranted)
                .ToListAsync();

            var groups = await _context.PermissionGroups
                .Include(pg => pg.PermissionGroupPermissions)
                .OrderBy(pg => pg.Name)
                .ToListAsync();

            var userGroups = await _context.UserPermissionGroups
                .Where(ug => ug.UserId == id)
                .Include(ug => ug.PermissionGroup)
                    .ThenInclude(pg => pg.PermissionGroupPermissions)
                .ToListAsync();

            var assignedGroupIds = userGroups
                .Select(ug => ug.PermissionGroupId)
                .ToHashSet();

            var inheritedPermissionIds = userGroups
                .SelectMany(ug => ug.PermissionGroup.PermissionGroupPermissions)
                .Select(pgp => pgp.PermissionId)
                .ToHashSet();

            var directPermissionIds = userPermissions
                .Select(up => up.PermissionId)
                .ToHashSet();

            var model = new ManageUserPermissionsViewModel
            {
                UserId = user.Id,
                UserName = user.FullName ?? user.Email ?? string.Empty,
                Permissions = permissions.Select(p => new PermissionSelectionViewModel
                {
                    PermissionId = p.Id,
                    DisplayName = p.DisplayName,
                    Category = p.Category,
                    IsGranted = directPermissionIds.Contains(p.Id) || inheritedPermissionIds.Contains(p.Id),
                    IsInherited = inheritedPermissionIds.Contains(p.Id),
                    HasDirectGrant = directPermissionIds.Contains(p.Id)
                }).ToList(),
                Groups = groups.Select(g => new PermissionGroupSelectionViewModel
                {
                    PermissionGroupId = g.Id,
                    Name = g.Name,
                    Description = g.Description,
                    PermissionsCount = g.PermissionGroupPermissions.Count,
                    IsAssigned = assignedGroupIds.Contains(g.Id),
                    PermissionIds = g.PermissionGroupPermissions
                        .Select(pgp => pgp.PermissionId)
                        .ToList()
                }).ToList()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "users.edit")]
        public async Task<IActionResult> ManagePermissions(ManageUserPermissionsViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.UserId);
            if (user == null) return NotFound();

            model.Groups ??= new List<PermissionGroupSelectionViewModel>();
            model.Permissions ??= new List<PermissionSelectionViewModel>();

            var selectedGroupIds = model.Groups
                .Where(g => g.IsAssigned)
                .Select(g => g.PermissionGroupId)
                .ToList();

            var existingGroups = await _context.UserPermissionGroups
                .Where(ug => ug.UserId == model.UserId)
                .ToListAsync();

            _context.UserPermissionGroups.RemoveRange(existingGroups);

            foreach (var groupId in selectedGroupIds)
            {
                _context.UserPermissionGroups.Add(new UserPermissionGroup
                {
                    UserId = model.UserId,
                    PermissionGroupId = groupId,
                    AssignedAt = DateTime.Now
                });
            }

            var inheritedPermissionIds = await _context.PermissionGroupPermissions
                .Where(pgp => selectedGroupIds.Contains(pgp.PermissionGroupId))
                .Select(pgp => pgp.PermissionId)
                .ToListAsync();

            var inheritedSet = inheritedPermissionIds.ToHashSet();

            var existing = await _context.UserPermissions
                .Where(up => up.UserId == model.UserId)
                .ToListAsync();

            _context.UserPermissions.RemoveRange(existing);

            foreach (var perm in model.Permissions.Where(p => p.IsGranted))
            {
                if (inheritedSet.Contains(perm.PermissionId))
                {
                    continue;
                }

                _context.UserPermissions.Add(new UserPermission
                {
                    UserId = model.UserId,
                    PermissionId = perm.PermissionId,
                    IsGranted = true,
                    CreatedAt = DateTime.Now
                });
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث صلاحيات المستخدم بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "users.edit")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleActive(string id)
        {
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            user.IsActive = !user.IsActive;
            await _userManager.UpdateAsync(user);
            if (Request.Headers["X-Requested-With"] == "XMLHttpRequest")
            {
                return Json(new { success = true, isActive = user.IsActive });
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "users.edit")]
        public async Task<IActionResult> ResetPassword(string id)
        {
            if (string.IsNullOrEmpty(id)) return NotFound();
            var user = await _userManager.FindByIdAsync(id);
            if (user == null) return NotFound();

            var model = new ResetUserPasswordViewModel { Id = user.Id };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "users.edit")]
        public async Task<IActionResult> ResetPassword(ResetUserPasswordViewModel model)
        {
            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            var token = await _userManager.GeneratePasswordResetTokenAsync(user);
            var result = await _userManager.ResetPasswordAsync(user, token, model.NewPassword);
            if (result.Succeeded)
            {
                TempData["SuccessMessage"] = "تم تحديث كلمة المرور بنجاح.";
                return RedirectToAction(nameof(Index));
            }
            foreach (var error in result.Errors)
            {
                ModelState.AddModelError(string.Empty, error.Description);
            }
            return View(model);
        }
    }
}
