using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.DynamicScreens;
using AccountingSystem.Models.Workflows;
using AccountingSystem.ViewModels.DynamicScreens;
using AccountingSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AccountingSystem.Controllers
{
    [Authorize]
    public class DynamicScreenEntriesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IAuthorizationService _authorizationService;
        private readonly IWorkflowService _workflowService;

        public DynamicScreenEntriesController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IAuthorizationService authorizationService,
            IWorkflowService workflowService)
        {
            _context = context;
            _userManager = userManager;
            _authorizationService = authorizationService;
            _workflowService = workflowService;
        }

        public async Task<IActionResult> Index(int id)
        {
            var screen = await _context.DynamicScreenDefinitions
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
            if (screen == null)
            {
                return NotFound();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var authorizationResult = await _authorizationService.AuthorizeAsync(User, screen.ManagePermissionName);
            var canManage = authorizationResult.Succeeded;

            var entriesQuery = _context.DynamicScreenEntries
                .Include(e => e.Supplier)
                .Include(e => e.ExpenseAccount)
                .Include(e => e.Branch)
                .Include(e => e.CreatedBy)
                .Where(e => e.ScreenId == id);

            if (!canManage)
            {
                entriesQuery = entriesQuery.Where(e => e.CreatedById == user.Id);
            }

            var entries = await entriesQuery
                .OrderByDescending(e => e.CreatedAt)
                .AsNoTracking()
                .ToListAsync();

            var model = new DynamicScreenEntriesIndexViewModel
            {
                Screen = screen,
                Entries = entries,
                CanManage = canManage
            };

            return View(model);
        }

        public async Task<IActionResult> Create(int id)
        {
            var screen = await _context.DynamicScreenDefinitions
                .Include(s => s.Fields)
                .FirstOrDefaultAsync(s => s.Id == id && s.IsActive);
            if (screen == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, screen.PermissionName);
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var model = await BuildEntryViewModelAsync(screen, null);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DynamicScreenEntryInputModel input)
        {
            var screen = await _context.DynamicScreenDefinitions
                .Include(s => s.Fields)
                .FirstOrDefaultAsync(s => s.Id == input.ScreenId && s.IsActive);
            if (screen == null)
            {
                return NotFound();
            }

            var authResult = await _authorizationService.AuthorizeAsync(User, screen.PermissionName);
            if (!authResult.Succeeded)
            {
                return Forbid();
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            NormalizeFieldInputs(screen, input);

            var orderedFields = screen.Fields.OrderBy(f => f.DisplayOrder).ToList();
            var data = new Dictionary<string, object?>();

            foreach (var field in orderedFields)
            {
                var valueModel = input.Fields.FirstOrDefault(f => f.FieldId == field.Id);
                var value = valueModel?.Value?.Trim();
                var fieldIndex = input.Fields.IndexOf(valueModel ?? new DynamicScreenEntryFieldValue { FieldId = field.Id });
                var key = fieldIndex >= 0 ? $"Fields[{fieldIndex}].Value" : string.Empty;

                if (field.IsRequired && string.IsNullOrWhiteSpace(value))
                {
                    ModelState.AddModelError(key, $"الحقل {field.Label} إجباري");
                    continue;
                }

                if (string.IsNullOrWhiteSpace(value))
                {
                    data[field.FieldKey] = null;
                    continue;
                }

                switch (field.FieldType)
                {
                    case DynamicScreenFieldType.Number:
                        if (!decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var numberValue))
                        {
                            ModelState.AddModelError(key, $"قيمة غير صحيحة للحقل {field.Label}");
                            continue;
                        }
                        data[field.FieldKey] = numberValue;
                        ApplyRoleValue(field, numberValue, input);
                        break;
                    case DynamicScreenFieldType.Date:
                        if (!DateTime.TryParse(value, CultureInfo.InvariantCulture, DateTimeStyles.None, out var dateValue))
                        {
                            ModelState.AddModelError(key, $"تاريخ غير صالح للحقل {field.Label}");
                            continue;
                        }
                        data[field.FieldKey] = dateValue;
                        break;
                    case DynamicScreenFieldType.Toggle:
                        data[field.FieldKey] = value.Equals("true", StringComparison.OrdinalIgnoreCase);
                        break;
                    case DynamicScreenFieldType.Select:
                        var selectResult = await HandleSelectFieldAsync(field, value);
                        if (!selectResult.IsSuccess)
                        {
                            ModelState.AddModelError(key, selectResult.ErrorMessage ?? $"قيمة غير صالحة للحقل {field.Label}");
                            continue;
                        }
                        data[field.FieldKey] = selectResult.Value;
                        ApplyRoleValue(field, selectResult.Value, input);
                        break;
                    default:
                        data[field.FieldKey] = value;
                        ApplyRoleValue(field, value, input);
                        break;
                }
            }

            if (!ModelState.IsValid)
            {
                var viewModel = await BuildEntryViewModelAsync(screen, input);
                return View(viewModel);
            }

            var entry = new DynamicScreenEntry
            {
                ScreenId = screen.Id,
                ScreenType = screen.ScreenType,
                CreatedAt = DateTime.UtcNow,
                CreatedById = user.Id,
                BranchId = input.BranchId,
                IsCash = ResolvePaymentMode(screen.PaymentMode, input.IsCash),
                DataJson = JsonSerializer.Serialize(data)
            };

            PopulateEntryFromRoles(entry, input, screen);

            entry.Status = screen.WorkflowDefinitionId.HasValue
                ? DynamicScreenEntryStatus.PendingApproval
                : DynamicScreenEntryStatus.Approved;

            if (entry.Status == DynamicScreenEntryStatus.Approved)
            {
                entry.ApprovedAt = DateTime.UtcNow;
                entry.ApprovedById = user.Id;
            }

            _context.DynamicScreenEntries.Add(entry);
            await _context.SaveChangesAsync();

            if (screen.WorkflowDefinitionId.HasValue)
            {
                var definition = await _context.WorkflowDefinitions
                    .Include(d => d.Steps)
                    .FirstOrDefaultAsync(d => d.Id == screen.WorkflowDefinitionId.Value);

                if (definition != null)
                {
                    var instance = await _workflowService.StartWorkflowAsync(
                        definition,
                        WorkflowDocumentType.DynamicScreenEntry,
                        entry.Id,
                        user.Id,
                        entry.BranchId,
                        entry.Amount,
                        entry.Amount,
                        null);

                    if (instance != null)
                    {
                        entry.WorkflowInstanceId = instance.Id;
                        await _context.SaveChangesAsync();
                    }
                    else
                    {
                        entry.Status = DynamicScreenEntryStatus.Approved;
                        entry.ApprovedAt = DateTime.UtcNow;
                        entry.ApprovedById = user.Id;
                        await _context.SaveChangesAsync();
                    }
                }
                else
                {
                    entry.Status = DynamicScreenEntryStatus.Approved;
                    entry.ApprovedAt = DateTime.UtcNow;
                    entry.ApprovedById = user.Id;
                    await _context.SaveChangesAsync();
                }
            }

            TempData["Success"] = "تم تسجيل الحركة بنجاح";
            return RedirectToAction(nameof(Index), new { id = screen.Id });
        }

        private async Task<DynamicScreenEntryViewModel> BuildEntryViewModelAsync(DynamicScreenDefinition screen, DynamicScreenEntryInputModel? input)
        {
            input ??= new DynamicScreenEntryInputModel { ScreenId = screen.Id };
            NormalizeFieldInputs(screen, input);

            if (screen.PaymentMode == DynamicScreenPaymentMode.CashOnly)
            {
                input.IsCash = true;
            }
            else if (screen.PaymentMode == DynamicScreenPaymentMode.NonCashOnly)
            {
                input.IsCash = false;
            }

            var viewModel = new DynamicScreenEntryViewModel
            {
                Screen = screen,
                Input = input,
                Fields = new List<DynamicScreenEntryFieldViewModel>()
            };

            var branches = await _context.Branches
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr,
                    Selected = input.BranchId.HasValue && input.BranchId.Value == b.Id
                })
                .ToListAsync();
            viewModel.Branches = branches;

            var accounts = screen.Fields.Any(f => f.DataSource == DynamicScreenFieldDataSource.Accounts)
                ? await _context.Accounts
                    .OrderBy(a => a.Code)
                    .Select(a => new ValueTuple<int, string>(a.Id, a.Code + " - " + a.NameAr))
                    .ToListAsync()
                : new List<(int, string)>();

            var suppliers = screen.Fields.Any(f => f.DataSource == DynamicScreenFieldDataSource.Suppliers)
                ? await _context.Suppliers
                    .OrderBy(s => s.NameAr)
                    .Select(s => new ValueTuple<int, string>(s.Id, s.NameAr))
                    .ToListAsync()
                : new List<(int, string)>();

            var expenses = screen.Fields.Any(f => f.DataSource == DynamicScreenFieldDataSource.Expenses)
                ? await _context.Accounts
                    .Where(a => a.AccountType == AccountType.Expenses)
                    .OrderBy(a => a.Code)
                    .Select(a => new ValueTuple<int, string>(a.Id, a.Code + " - " + a.NameAr))
                    .ToListAsync()
                : new List<(int, string)>();

            var assets = screen.Fields.Any(f => f.DataSource == DynamicScreenFieldDataSource.Assets)
                ? await _context.Assets
                    .OrderBy(a => a.Name)
                    .Select(a => new ValueTuple<int, string>(a.Id, a.Name))
                    .ToListAsync()
                : new List<(int, string)>();

            var employees = screen.Fields.Any(f => f.DataSource == DynamicScreenFieldDataSource.Employees)
                ? await _context.Employees
                    .OrderBy(e => e.Name)
                    .Select(e => new ValueTuple<int, string>(e.Id, e.Name))
                    .ToListAsync()
                : new List<(int, string)>();

            foreach (var field in screen.Fields.OrderBy(f => f.DisplayOrder))
            {
                var valueModel = input.Fields.First(f => f.FieldId == field.Id);
                var options = BuildOptions(field, valueModel.Value, accounts, suppliers, expenses, assets, employees);
                viewModel.Fields.Add(new DynamicScreenEntryFieldViewModel
                {
                    Field = field,
                    Options = options
                });
            }

            return viewModel;
        }

        private static void NormalizeFieldInputs(DynamicScreenDefinition screen, DynamicScreenEntryInputModel input)
        {
            if (input.Fields == null)
            {
                input.Fields = new List<DynamicScreenEntryFieldValue>();
            }

            foreach (var field in screen.Fields)
            {
                if (!input.Fields.Any(f => f.FieldId == field.Id))
                {
                    input.Fields.Add(new DynamicScreenEntryFieldValue { FieldId = field.Id });
                }
            }

            input.Fields = screen.Fields
                .OrderBy(f => f.DisplayOrder)
                .Select(f => input.Fields.First(v => v.FieldId == f.Id))
                .ToList();
        }

        private IEnumerable<SelectListItem> BuildOptions(
            DynamicScreenField field,
            string? selectedValue,
            List<(int Id, string Name)> accounts,
            List<(int Id, string Name)> suppliers,
            List<(int Id, string Name)> expenses,
            List<(int Id, string Name)> assets,
            List<(int Id, string Name)> employees)
        {
            var allowedIds = ParseAllowedIds(field);
            IEnumerable<SelectListItem> result;
            switch (field.DataSource)
            {
                case DynamicScreenFieldDataSource.Accounts:
                    result = accounts
                        .Where(a => !allowedIds.Any() || allowedIds.Contains(a.Id))
                        .Select(a => new SelectListItem
                        {
                            Value = a.Id.ToString(),
                            Text = a.Name,
                            Selected = selectedValue == a.Id.ToString()
                        });
                    break;
                case DynamicScreenFieldDataSource.Suppliers:
                    result = suppliers
                        .Where(s => !allowedIds.Any() || allowedIds.Contains(s.Id))
                        .Select(s => new SelectListItem
                        {
                            Value = s.Id.ToString(),
                            Text = s.Name,
                            Selected = selectedValue == s.Id.ToString()
                        });
                    break;
                case DynamicScreenFieldDataSource.Expenses:
                    result = expenses
                        .Where(e => !allowedIds.Any() || allowedIds.Contains(e.Id))
                        .Select(e => new SelectListItem
                        {
                            Value = e.Id.ToString(),
                            Text = e.Name,
                            Selected = selectedValue == e.Id.ToString()
                        });
                    break;
                case DynamicScreenFieldDataSource.Assets:
                    result = assets
                        .Where(a => !allowedIds.Any() || allowedIds.Contains(a.Id))
                        .Select(a => new SelectListItem
                        {
                            Value = a.Id.ToString(),
                            Text = a.Name,
                            Selected = selectedValue == a.Id.ToString()
                        });
                    break;
                case DynamicScreenFieldDataSource.Employees:
                    result = employees
                        .Where(e => !allowedIds.Any() || allowedIds.Contains(e.Id))
                        .Select(e => new SelectListItem
                        {
                            Value = e.Id.ToString(),
                            Text = e.Name,
                            Selected = selectedValue == e.Id.ToString()
                        });
                    break;
                case DynamicScreenFieldDataSource.CustomOptions:
                    result = ParseCustomOptions(field.MetadataJson, selectedValue);
                    break;
                default:
                    result = Array.Empty<SelectListItem>();
                    break;
            }

            return result;
        }

        private async Task<SelectFieldResult> HandleSelectFieldAsync(DynamicScreenField field, string value)
        {
            var allowedIds = ParseAllowedIds(field);

            switch (field.DataSource)
            {
                case DynamicScreenFieldDataSource.Accounts:
                    if (!int.TryParse(value, out var accountId))
                    {
                        return SelectFieldResult.FromError($"قيمة غير صحيحة للحقل {field.Label}");
                    }
                    if (allowedIds.Any() && !allowedIds.Contains(accountId))
                    {
                        return SelectFieldResult.FromError($"القيمة المختارة غير مسموح بها للحقل {field.Label}");
                    }
                    if (!await _context.Accounts.AnyAsync(a => a.Id == accountId))
                    {
                        return SelectFieldResult.FromError($"الحساب المحدد غير موجود");
                    }
                    return SelectFieldResult.FromSuccess(accountId);
                case DynamicScreenFieldDataSource.Suppliers:
                    if (!int.TryParse(value, out var supplierId))
                    {
                        return SelectFieldResult.FromError($"قيمة غير صحيحة للحقل {field.Label}");
                    }
                    if (allowedIds.Any() && !allowedIds.Contains(supplierId))
                    {
                        return SelectFieldResult.FromError($"المورد المحدد غير مسموح به لهذا الحقل");
                    }
                    if (!await _context.Suppliers.AnyAsync(s => s.Id == supplierId))
                    {
                        return SelectFieldResult.FromError($"المورد المحدد غير موجود");
                    }
                    return SelectFieldResult.FromSuccess(supplierId);
                case DynamicScreenFieldDataSource.Expenses:
                    if (!int.TryParse(value, out var expenseId))
                    {
                        return SelectFieldResult.FromError($"قيمة غير صحيحة للحقل {field.Label}");
                    }
                    if (allowedIds.Any() && !allowedIds.Contains(expenseId))
                    {
                        return SelectFieldResult.FromError($"القيمة المحددة غير مسموح بها للحقل {field.Label}");
                    }
                    if (!await _context.Expenses.AnyAsync(e => e.Id == expenseId))
                    {
                        return SelectFieldResult.FromError($"المصروف المحدد غير موجود");
                    }
                    return SelectFieldResult.FromSuccess(expenseId);
                case DynamicScreenFieldDataSource.Assets:
                    if (!int.TryParse(value, out var assetId))
                    {
                        return SelectFieldResult.FromError($"قيمة غير صحيحة للحقل {field.Label}");
                    }
                    if (allowedIds.Any() && !allowedIds.Contains(assetId))
                    {
                        return SelectFieldResult.FromError($"القيمة المحددة غير مسموح بها للحقل {field.Label}");
                    }
                    if (!await _context.Assets.AnyAsync(a => a.Id == assetId))
                    {
                        return SelectFieldResult.FromError($"الأصل المحدد غير موجود");
                    }
                    return SelectFieldResult.FromSuccess(assetId);
                case DynamicScreenFieldDataSource.Employees:
                    if (!int.TryParse(value, out var employeeId))
                    {
                        return SelectFieldResult.FromError($"قيمة غير صحيحة للحقل {field.Label}");
                    }
                    if (allowedIds.Any() && !allowedIds.Contains(employeeId))
                    {
                        return SelectFieldResult.FromError($"الموظف المحدد غير مسموح به");
                    }
                    if (!await _context.Employees.AnyAsync(e => e.Id == employeeId))
                    {
                        return SelectFieldResult.FromError($"الموظف المحدد غير موجود");
                    }
                    return SelectFieldResult.FromSuccess(employeeId);
                case DynamicScreenFieldDataSource.CustomOptions:
                    var options = ParseCustomOptions(field.MetadataJson, null).Select(o => o.Value).ToHashSet(StringComparer.OrdinalIgnoreCase);
                    if (!options.Contains(value))
                    {
                        return SelectFieldResult.FromError($"القيمة المحددة غير مسموح بها");
                    }
                    return SelectFieldResult.FromSuccess(value);
                default:
                    return SelectFieldResult.FromSuccess(value);
            }
        }

        private static IEnumerable<SelectListItem> ParseCustomOptions(string? metadata, string? selectedValue)
        {
            if (string.IsNullOrWhiteSpace(metadata))
            {
                return Array.Empty<SelectListItem>();
            }

            var lines = metadata.Split(new[] { '\r', '\n' }, StringSplitOptions.RemoveEmptyEntries);
            var result = new List<SelectListItem>();
            foreach (var line in lines)
            {
                var parts = line.Split('|', 2);
                var value = parts[0].Trim();
                var text = parts.Length > 1 ? parts[1].Trim() : value;
                result.Add(new SelectListItem
                {
                    Value = value,
                    Text = text,
                    Selected = string.Equals(selectedValue, value, StringComparison.OrdinalIgnoreCase)
                });
            }

            return result;
        }

        private static HashSet<int> ParseAllowedIds(DynamicScreenField field)
        {
            if (string.IsNullOrWhiteSpace(field.AllowedEntityIds))
            {
                return new HashSet<int>();
            }

            var result = new HashSet<int>();
            var parts = field.AllowedEntityIds.Split(',', StringSplitOptions.RemoveEmptyEntries);
            foreach (var part in parts)
            {
                if (int.TryParse(part.Trim(), out var value))
                {
                    result.Add(value);
                }
            }

            return result;
        }

        private static void ApplyRoleValue(DynamicScreenField field, object? value, DynamicScreenEntryInputModel input)
        {
            if (field.Role == DynamicScreenFieldRole.Branch && value is int branchId)
            {
                input.BranchId = branchId;
            }
        }

        private static bool ResolvePaymentMode(DynamicScreenPaymentMode mode, bool selectedIsCash)
        {
            return mode switch
            {
                DynamicScreenPaymentMode.CashOnly => true,
                DynamicScreenPaymentMode.NonCashOnly => false,
                _ => selectedIsCash
            };
        }

        private static void PopulateEntryFromRoles(DynamicScreenEntry entry, DynamicScreenEntryInputModel input, DynamicScreenDefinition screen)
        {
            foreach (var field in screen.Fields)
            {
                var value = input.Fields.FirstOrDefault(f => f.FieldId == field.Id)?.Value;
                if (string.IsNullOrWhiteSpace(value))
                {
                    continue;
                }

                switch (field.Role)
                {
                    case DynamicScreenFieldRole.Amount:
                        if (decimal.TryParse(value, NumberStyles.Number, CultureInfo.InvariantCulture, out var amount))
                        {
                            entry.Amount = amount;
                        }
                        break;
                    case DynamicScreenFieldRole.Description:
                        entry.Description = value;
                        break;
                    case DynamicScreenFieldRole.Supplier:
                        if (int.TryParse(value, out var supplierId))
                        {
                            entry.SupplierId = supplierId;
                        }
                        break;
                    case DynamicScreenFieldRole.ExpenseAccount:
                        if (int.TryParse(value, out var accountId))
                        {
                            entry.ExpenseAccountId = accountId;
                        }
                        break;
                    case DynamicScreenFieldRole.Branch:
                        if (int.TryParse(value, out var branchId))
                        {
                            entry.BranchId = branchId;
                        }
                        break;
                    default:
                        break;
                }
            }
        }

        private record SelectFieldResult(bool IsSuccess, object? Value, string? ErrorMessage)
        {
            public static SelectFieldResult FromSuccess(object? value) => new(true, value, null);
            public static SelectFieldResult FromError(string message) => new(false, null, message);
        }
    }
}
