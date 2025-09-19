using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using System.Linq;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "users.view")]

    public class UsersController : Controller
    {
        private readonly UserManager<User> _userManager;
        private readonly ApplicationDbContext _context;

        public UsersController(UserManager<User> userManager, ApplicationDbContext context)
        {
            _userManager = userManager;
            _context = context;
        }




        [Authorize(Policy = "users.view")]


        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Policy = "users.view")]
        [HttpGet]
        public async Task<IActionResult> GridData([FromQuery] SlickGridRequest request)
        {
            var sanitizedPage = request.GetValidatedPage();
            var sanitizedPageSize = request.GetValidatedPageSize();

            var query = _userManager.Users
                .AsNoTracking()
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(request.Search))
            {
                var term = request.Search.Trim();
                var likeExpression = $"%{term}%";
                query = query.Where(u =>
                    EF.Functions.Like(u.Email ?? string.Empty, likeExpression) ||
                    EF.Functions.Like(u.FirstName, likeExpression) ||
                    EF.Functions.Like(u.LastName, likeExpression));
            }

            query = (request.SortField?.ToLowerInvariant()) switch
            {
                "fullname" => request.IsDescending
                    ? query.OrderByDescending(u => u.FirstName).ThenByDescending(u => u.LastName)
                    : query.OrderBy(u => u.FirstName).ThenBy(u => u.LastName),
                "isactive" => request.IsDescending
                    ? query.OrderByDescending(u => u.IsActive)
                    : query.OrderBy(u => u.IsActive),
                "lastloginat" => request.IsDescending
                    ? query.OrderByDescending(u => u.LastLoginAt)
                    : query.OrderBy(u => u.LastLoginAt),
                _ => request.IsDescending
                    ? query.OrderByDescending(u => u.Email)
                    : query.OrderBy(u => u.Email)
            };

            var totalCount = await query.CountAsync();

            var items = await query
                .Skip((sanitizedPage - 1) * sanitizedPageSize)
                .Take(sanitizedPageSize)
                .Select(u => new UserListViewModel
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    FullName = u.FirstName + " " + u.LastName,
                    IsActive = u.IsActive,
                    LastLoginAt = u.LastLoginAt
                })
                .ToListAsync();

            var response = new SlickGridResponse<UserListViewModel>
            {
                TotalCount = totalCount,
                Items = items
            };

            return Json(response);
        }

        [Authorize(Policy = "users.create")]
        public async Task<IActionResult> Create()
        {
            var model = new CreateUserViewModel();
            model.Branches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                }).ToListAsync();
            model.PaymentBranches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                }).ToListAsync();
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
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "users.create")]
        public async Task<IActionResult> Create(CreateUserViewModel model)
        {
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
                    ExpenseLimit = model.ExpenseLimit
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
            model.Branches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                }).ToListAsync();
            model.PaymentBranches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                }).ToListAsync();
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
                ExpenseLimit = user.ExpenseLimit
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
            if (!ModelState.IsValid) return View(model);

            var user = await _userManager.FindByIdAsync(model.Id);
            if (user == null) return NotFound();

            user.Email = model.Email;
            user.UserName = model.Email;
            user.FirstName = model.FirstName;
            user.LastName = model.LastName;
            user.IsActive = model.IsActive;
            user.PaymentBranchId = model.PaymentBranchId;
            user.ExpenseLimit = model.ExpenseLimit;

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
            var userAccountsEdit = await _context.UserPaymentAccounts
                .Where(upa => upa.UserId == model.Id)
                .ToListAsync();
            var currenciesEdit = await _context.Currencies.ToListAsync();
            model.CurrencyAccounts = new List<UserCurrencyAccountViewModel>();
            foreach (var currency in currenciesEdit)
            {
                var accountList = await _context.Accounts
                    .Where(a => a.CanPostTransactions && a.CurrencyId == currency.Id)
                    .ToListAsync();

                var accounts = accountList.Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}",
                    Selected = userAccountsEdit.Any(ua => ua.CurrencyId == currency.Id && ua.AccountId == a.Id)
                }).ToList();
                model.CurrencyAccounts.Add(new UserCurrencyAccountViewModel
                {
                    CurrencyId = currency.Id,
                    CurrencyName = currency.Name,
                    AccountId = userAccountsEdit.FirstOrDefault(ua => ua.CurrencyId == currency.Id)?.AccountId,
                    Accounts = accounts
                });
            }
            return View(model);
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

            var model = new ManageUserPermissionsViewModel
            {
                UserId = user.Id,
                UserName = user.FullName ?? user.Email ?? string.Empty,
                Permissions = permissions.Select(p => new PermissionSelectionViewModel
                {
                    PermissionId = p.Id,
                    DisplayName = p.DisplayName,
                    Category = p.Category,
                    IsGranted = userPermissions.Any(up => up.PermissionId == p.Id)
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

            var existing = await _context.UserPermissions
                .Where(up => up.UserId == model.UserId)
                .ToListAsync();

            _context.UserPermissions.RemoveRange(existing);

            foreach (var perm in model.Permissions.Where(p => p.IsGranted))
            {
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
