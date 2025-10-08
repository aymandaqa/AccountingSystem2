using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "permissiongroups.view")]
    public class PermissionGroupsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public PermissionGroupsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var groups = await _context.PermissionGroups
                .Select(group => new PermissionGroupListItemViewModel
                {
                    Id = group.Id,
                    Name = group.Name,
                    Description = group.Description,
                    PermissionsCount = group.PermissionGroupPermissions.Count,
                    UsersCount = group.UserPermissionGroups.Count,
                    CreatedAt = group.CreatedAt
                })
                .OrderBy(g => g.Name)
                .ToListAsync();

            return View(groups);
        }

        [Authorize(Policy = "permissiongroups.create")]
        public async Task<IActionResult> Create()
        {
            var model = await BuildEditViewModelAsync(new EditPermissionGroupViewModel());
            return View("Edit", model);
        }

        [Authorize(Policy = "permissiongroups.create")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(EditPermissionGroupViewModel model)
        {
            if (!ModelState.IsValid)
            {
                model = await BuildEditViewModelAsync(model);
                return View("Edit", model);
            }

            var group = new PermissionGroup
            {
                Name = model.Name.Trim(),
                Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim(),
                CreatedAt = DateTime.Now
            };

            var selectedPermissionIds = model.Permissions
                .Where(p => p.IsGranted)
                .Select(p => p.PermissionId)
                .ToList();

            foreach (var permissionId in selectedPermissionIds)
            {
                group.PermissionGroupPermissions.Add(new PermissionGroupPermission
                {
                    PermissionId = permissionId
                });
            }

            _context.PermissionGroups.Add(group);
            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم إنشاء مجموعة الصلاحيات بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "permissiongroups.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var group = await _context.PermissionGroups
                .Include(pg => pg.PermissionGroupPermissions)
                .FirstOrDefaultAsync(pg => pg.Id == id);

            if (group == null)
            {
                return NotFound();
            }

            var model = new EditPermissionGroupViewModel
            {
                Id = group.Id,
                Name = group.Name,
                Description = group.Description
            };

            model = await BuildEditViewModelAsync(model, group.PermissionGroupPermissions.Select(p => p.PermissionId));
            return View(model);
        }

        [Authorize(Policy = "permissiongroups.edit")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, EditPermissionGroupViewModel model)
        {
            if (id != model.Id)
            {
                return BadRequest();
            }

            if (!ModelState.IsValid)
            {
                model = await BuildEditViewModelAsync(model);
                return View(model);
            }

            var group = await _context.PermissionGroups
                .Include(pg => pg.PermissionGroupPermissions)
                .FirstOrDefaultAsync(pg => pg.Id == id);

            if (group == null)
            {
                return NotFound();
            }

            group.Name = model.Name.Trim();
            group.Description = string.IsNullOrWhiteSpace(model.Description) ? null : model.Description.Trim();

            var selectedPermissionIds = model.Permissions
                .Where(p => p.IsGranted)
                .Select(p => p.PermissionId)
                .ToHashSet();

            var existingPermissions = group.PermissionGroupPermissions.ToList();

            foreach (var permission in existingPermissions)
            {
                if (!selectedPermissionIds.Contains(permission.PermissionId))
                {
                    _context.PermissionGroupPermissions.Remove(permission);
                }
            }

            foreach (var permissionId in selectedPermissionIds)
            {
                if (!existingPermissions.Any(p => p.PermissionId == permissionId))
                {
                    group.PermissionGroupPermissions.Add(new PermissionGroupPermission
                    {
                        PermissionGroupId = group.Id,
                        PermissionId = permissionId
                    });
                }
            }

            await _context.SaveChangesAsync();
            TempData["SuccessMessage"] = "تم تحديث مجموعة الصلاحيات بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "permissiongroups.delete")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id)
        {
            var group = await _context.PermissionGroups
                .Include(pg => pg.UserPermissionGroups)
                .FirstOrDefaultAsync(pg => pg.Id == id);

            if (group == null)
            {
                return Json(new { success = false, message = "لم يتم العثور على مجموعة الصلاحيات." });
            }

            if (group.UserPermissionGroups.Any())
            {
                return Json(new { success = false, message = "لا يمكن حذف مجموعة مرتبطة بمستخدمين." });
            }

            _context.PermissionGroups.Remove(group);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        private async Task<EditPermissionGroupViewModel> BuildEditViewModelAsync(EditPermissionGroupViewModel model, IEnumerable<int>? selectedIds = null)
        {
            var selectedSet = selectedIds != null
                ? new HashSet<int>(selectedIds)
                : new HashSet<int>();

            var permissions = await _context.Permissions
                .OrderBy(p => p.Category)
                .ThenBy(p => p.DisplayName)
                .Select(p => new PermissionSelectionViewModel
                {
                    PermissionId = p.Id,
                    DisplayName = p.DisplayName,
                    Category = p.Category,
                    IsGranted = selectedSet.Contains(p.Id)
                })
                .ToListAsync();

            model.Permissions = permissions;
            return model;
        }
    }
}
