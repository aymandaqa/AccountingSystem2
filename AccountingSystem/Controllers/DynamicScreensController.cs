using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.DynamicScreens;
using AccountingSystem.Models.Workflows;
using AccountingSystem.ViewModels.DynamicScreens;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "dynamicscreens.manage")]
    public class DynamicScreensController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public DynamicScreensController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var screens = await _context.DynamicScreenDefinitions
                .Include(s => s.WorkflowDefinition)
                .AsNoTracking()
                .OrderBy(s => s.MenuOrder)
                .ThenBy(s => s.DisplayName)
                .ToListAsync();

            return View(screens);
        }

        public async Task<IActionResult> Create()
        {
            var model = new DynamicScreenEditViewModel
            {
                PaymentMode = DynamicScreenPaymentMode.CashAndNonCash,
                ScreenType = DynamicScreenType.Payment,
                Fields = new List<DynamicScreenFieldInputModel>()
            };

            await PopulateLookupsAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DynamicScreenEditViewModel model)
        {
            await ValidateFieldsAsync(model);

            if (!ModelState.IsValid)
            {
                await PopulateLookupsAsync();
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            var slug = GenerateSlug(model.Name);
            var permissionName = string.IsNullOrWhiteSpace(model.PermissionName)
                ? $"dynamicscreens.{slug}.use"
                : model.PermissionName.Trim().ToLowerInvariant();
            var managePermissionName = string.IsNullOrWhiteSpace(model.ManagePermissionName)
                ? $"dynamicscreens.{slug}.manage"
                : model.ManagePermissionName.Trim().ToLowerInvariant();

            var definition = new DynamicScreenDefinition
            {
                Name = slug,
                DisplayName = model.DisplayName,
                Description = model.Description,
                ScreenType = model.ScreenType,
                PaymentMode = model.PaymentMode,
                WorkflowDefinitionId = model.WorkflowDefinitionId,
                MenuOrder = model.MenuOrder,
                PermissionName = permissionName,
                ManagePermissionName = managePermissionName,
                CreatedAt = DateTime.UtcNow,
                CreatedById = user?.Id
            };

            definition.Fields = model.Fields
                .OrderBy(f => f.DisplayOrder)
                .Select(f => new DynamicScreenField
                {
                    FieldKey = f.FieldKey.Trim(),
                    Label = f.Label.Trim(),
                    FieldType = f.FieldType,
                    DataSource = f.DataSource,
                    Role = f.Role,
                    IsRequired = f.IsRequired,
                    DisplayOrder = f.DisplayOrder,
                    ColumnSpan = f.ColumnSpan,
                    Placeholder = f.Placeholder,
                    HelpText = f.HelpText,
                    AllowedEntityIds = NormalizeAllowedIds(f.AllowedEntityIds),
                    MetadataJson = f.MetadataJson
                })
                .ToList();

            _context.DynamicScreenDefinitions.Add(definition);
            await EnsurePermissionAsync(permissionName, $"استخدام شاشة {definition.DisplayName}");
            await EnsurePermissionAsync(managePermissionName, $"إدارة حركات شاشة {definition.DisplayName}");

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم إنشاء الشاشة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var definition = await _context.DynamicScreenDefinitions
                .Include(s => s.Fields)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (definition == null)
            {
                return NotFound();
            }

            var model = new DynamicScreenEditViewModel
            {
                Id = definition.Id,
                Name = definition.Name,
                DisplayName = definition.DisplayName,
                Description = definition.Description,
                ScreenType = definition.ScreenType,
                PaymentMode = definition.PaymentMode,
                WorkflowDefinitionId = definition.WorkflowDefinitionId,
                MenuOrder = definition.MenuOrder,
                PermissionName = definition.PermissionName,
                ManagePermissionName = definition.ManagePermissionName,
                Fields = definition.Fields
                    .OrderBy(f => f.DisplayOrder)
                    .Select(f => new DynamicScreenFieldInputModel
                    {
                        Id = f.Id,
                        FieldKey = f.FieldKey,
                        Label = f.Label,
                        FieldType = f.FieldType,
                        DataSource = f.DataSource,
                        Role = f.Role,
                        IsRequired = f.IsRequired,
                        DisplayOrder = f.DisplayOrder,
                        ColumnSpan = f.ColumnSpan,
                        Placeholder = f.Placeholder,
                        HelpText = f.HelpText,
                        AllowedEntityIds = f.AllowedEntityIds,
                        MetadataJson = f.MetadataJson
                    })
                    .ToList()
            };

            await PopulateLookupsAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, DynamicScreenEditViewModel model)
        {
            await ValidateFieldsAsync(model);

            var definition = await _context.DynamicScreenDefinitions
                .Include(s => s.Fields)
                .FirstOrDefaultAsync(s => s.Id == id);
            if (definition == null)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                await PopulateLookupsAsync();
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);

            definition.DisplayName = model.DisplayName;
            definition.Description = model.Description;
            definition.ScreenType = model.ScreenType;
            definition.PaymentMode = model.PaymentMode;
            definition.WorkflowDefinitionId = model.WorkflowDefinitionId;
            definition.MenuOrder = model.MenuOrder;
            definition.PermissionName = model.PermissionName?.Trim().ToLowerInvariant() ?? definition.PermissionName;
            definition.ManagePermissionName = model.ManagePermissionName?.Trim().ToLowerInvariant() ?? definition.ManagePermissionName;
            definition.UpdatedAt = DateTime.UtcNow;
            definition.UpdatedById = user?.Id;

            var fieldsToKeep = new HashSet<DynamicScreenField>();
            foreach (var fieldInput in model.Fields.OrderBy(f => f.DisplayOrder))
            {
                DynamicScreenField field;
                if (fieldInput.Id.HasValue)
                {
                    field = definition.Fields.FirstOrDefault(f => f.Id == fieldInput.Id.Value) ?? new DynamicScreenField();
                    if (!definition.Fields.Contains(field))
                    {
                        definition.Fields.Add(field);
                    }
                }
                else
                {
                    field = new DynamicScreenField();
                    definition.Fields.Add(field);
                }

                field.FieldKey = fieldInput.FieldKey.Trim();
                field.Label = fieldInput.Label.Trim();
                field.FieldType = fieldInput.FieldType;
                field.DataSource = fieldInput.DataSource;
                field.Role = fieldInput.Role;
                field.IsRequired = fieldInput.IsRequired;
                field.DisplayOrder = fieldInput.DisplayOrder;
                field.ColumnSpan = fieldInput.ColumnSpan;
                field.Placeholder = fieldInput.Placeholder;
                field.HelpText = fieldInput.HelpText;
                field.AllowedEntityIds = NormalizeAllowedIds(fieldInput.AllowedEntityIds);
                field.MetadataJson = fieldInput.MetadataJson;

                fieldsToKeep.Add(field);
            }

            var toRemove = definition.Fields
                .Where(f => !fieldsToKeep.Contains(f))
                .ToList();
            foreach (var field in toRemove)
            {
                _context.DynamicScreenFields.Remove(field);
            }

            await EnsurePermissionAsync(definition.PermissionName, $"استخدام شاشة {definition.DisplayName}");
            await EnsurePermissionAsync(definition.ManagePermissionName, $"إدارة حركات شاشة {definition.DisplayName}");

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم تحديث الشاشة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateLookupsAsync()
        {
            var workflowDefinitions = await _context.WorkflowDefinitions
                .Where(d => d.DocumentType == WorkflowDocumentType.DynamicScreenEntry && d.IsActive)
                .OrderBy(d => d.Name)
                .Select(d => new { d.Id, d.Name })
                .ToListAsync();

            ViewBag.WorkflowDefinitions = new SelectList(workflowDefinitions, "Id", "Name");

            var accounts = await _context.Accounts
                .OrderBy(a => a.Code)
                .Select(a => new { a.Id, Name = a.Code + " - " + a.NameAr })
                .ToListAsync();
            ViewBag.Accounts = new SelectList(accounts, "Id", "Name");

            var suppliers = await _context.Suppliers
                .OrderBy(s => s.NameAr)
                .Select(s => new { s.Id, s.NameAr })
                .ToListAsync();
            ViewBag.Suppliers = new SelectList(suppliers, "Id", "NameAr");

            var expenses = await _context.Accounts
                .Where(a => a.AccountType == AccountType.Expenses)
                .OrderBy(a => a.Code)
                .Select(a => new { a.Id, Name = a.Code + " - " + a.NameAr })
                .ToListAsync();
            ViewBag.Expenses = new SelectList(expenses, "Id", "Name");

            var assets = await _context.Assets
                .OrderBy(a => a.Name)
                .Select(a => new { a.Id, a.Name })
                .ToListAsync();
            ViewBag.Assets = new SelectList(assets, "Id", "Name");

            var employees = await _context.Employees
                .OrderBy(e => e.Name)
                .Select(e => new { e.Id, e.Name })
                .ToListAsync();
            ViewBag.Employees = new SelectList(employees, "Id", "Name");
        }

        private async Task ValidateFieldsAsync(DynamicScreenEditViewModel model)
        {
            if (model.Fields == null || !model.Fields.Any())
            {
                ModelState.AddModelError(nameof(model.Fields), "يجب إضافة حقل واحد على الأقل");
                return;
            }

            if (model.Fields.Count(f => f.Role == DynamicScreenFieldRole.Amount) != 1)
            {
                ModelState.AddModelError(string.Empty, "يجب تحديد حقل واحد فقط للمبلغ");
            }

            if (model.Fields.Count(f => f.Role == DynamicScreenFieldRole.Description) != 1)
            {
                ModelState.AddModelError(string.Empty, "يجب تحديد حقل واحد فقط للوصف");
            }

            if (model.ScreenType == DynamicScreenType.Payment && !model.Fields.Any(f => f.Role == DynamicScreenFieldRole.ExpenseAccount))
            {
                ModelState.AddModelError(string.Empty, "يجب تحديد حقل لحساب المصروفات");
            }

            if (!model.Fields.Any(f => f.DisplayOrder >= 0))
            {
                ModelState.AddModelError(string.Empty, "يجب تحديد ترتيب لحقول الشاشة");
            }
        }

        private static string NormalizeAllowedIds(string? raw)
        {
            if (string.IsNullOrWhiteSpace(raw))
            {
                return null;
            }

            var parts = raw.Split(',', StringSplitOptions.RemoveEmptyEntries)
                .Select(p => p.Trim())
                .Where(p => !string.IsNullOrWhiteSpace(p));

            return string.Join(',', parts);
        }

        private static string GenerateSlug(string source)
        {
            if (string.IsNullOrWhiteSpace(source))
            {
                return $"screen-{Guid.NewGuid():N}";
            }

            var normalized = source.Trim().ToLowerInvariant();
            normalized = normalized.Normalize(NormalizationForm.FormD);
            var builder = new StringBuilder();
            foreach (var ch in normalized)
            {
                var unicodeCategory = CharUnicodeInfo.GetUnicodeCategory(ch);
                if (unicodeCategory == UnicodeCategory.NonSpacingMark)
                {
                    continue;
                }

                if (char.IsLetterOrDigit(ch))
                {
                    builder.Append(ch);
                }
                else if (char.IsWhiteSpace(ch) || ch == '-' || ch == '_' || ch == '.')
                {
                    builder.Append('-');
                }
            }

            var slug = builder.ToString().Trim('-');
            if (string.IsNullOrWhiteSpace(slug))
            {
                slug = $"screen-{Guid.NewGuid():N}";
            }

            return slug;
        }

        private async Task EnsurePermissionAsync(string permissionName, string displayName)
        {
            if (string.IsNullOrWhiteSpace(permissionName))
            {
                return;
            }

            var permission = await _context.Permissions.FirstOrDefaultAsync(p => p.Name == permissionName);
            if (permission == null)
            {
                permission = new Permission
                {
                    Name = permissionName,
                    DisplayName = displayName,
                    Category = "الشاشات الديناميكية",
                    CreatedAt = DateTime.UtcNow
                };
                _context.Permissions.Add(permission);
            }
            else
            {
                permission.DisplayName = displayName;
                permission.Category = "الشاشات الديناميكية";
            }
        }
    }
}
