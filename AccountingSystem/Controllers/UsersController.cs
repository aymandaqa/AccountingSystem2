using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using System.Linq;

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

        public async Task<IActionResult> Index()
        {
            var users = await _userManager.Users
                .Select(u => new UserListViewModel
                {
                    Id = u.Id,
                    Email = u.Email ?? string.Empty,
                    FullName = u.FullName ?? string.Empty,
                    IsActive = u.IsActive
                }).ToListAsync();

            return View(users);
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
                    CreatedAt = DateTime.UtcNow
                });
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث صلاحيات المستخدم بنجاح.";
            return RedirectToAction(nameof(Index));
        }
    }
}
