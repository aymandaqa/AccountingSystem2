using System.Collections.Generic;
using System.Linq;
using System.Security.Claims;
using System.Text.Json;
using System.Text.Json.Serialization;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.CompoundJournals;
using AccountingSystem.Services;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "journal.view")]
    public class CompoundJournalDefinitionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICompoundJournalService _compoundJournalService;
        private readonly JsonSerializerOptions _jsonOptions;

        public CompoundJournalDefinitionsController(ApplicationDbContext context, ICompoundJournalService compoundJournalService)
        {
            _context = context;
            _compoundJournalService = compoundJournalService;
            _jsonOptions = new JsonSerializerOptions
            {
                WriteIndented = true,
                PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
                DictionaryKeyPolicy = JsonNamingPolicy.CamelCase,
                Converters = { new JsonStringEnumConverter() }
            };
        }

        public async Task<IActionResult> Index()
        {
            var definitions = await _context.CompoundJournalDefinitions
                .AsNoTracking()
                .OrderByDescending(d => d.CreatedAtUtc)
                .ToListAsync();
            return View(definitions);
        }

        public async Task<IActionResult> Details(int id)
        {
            var definition = await _context.CompoundJournalDefinitions
                .Include(d => d.ExecutionLogs)
                .AsNoTracking()
                .FirstOrDefaultAsync(d => d.Id == id);

            if (definition == null)
            {
                return NotFound();
            }

            try
            {
                ViewBag.TemplatePreview = JsonSerializer.Serialize(
                    await _compoundJournalService.ParseTemplateAsync(definition.TemplateJson),
                    _jsonOptions);
            }
            catch
            {
                ViewBag.TemplatePreview = definition.TemplateJson;
            }

            ViewBag.ExecutionLogs = definition.ExecutionLogs
                .OrderByDescending(l => l.ExecutedAtUtc)
                .Take(100)
                .ToList();

            return View(definition);
        }

        [Authorize(Policy = "journal.create")]
        public async Task<IActionResult> Create()
        {
            var sampleTemplate = JsonSerializer.Serialize(new CompoundJournalTemplate
            {
                Description = "قيد مركب تجريبي",
                Lines = new List<CompoundJournalLineTemplate>
                {
                    new()
                    {
                        AccountId = 1,
                        Description = "بند مدين",
                        Debit = new TemplateValue { Type = TemplateValueType.Fixed, FixedValue = 1000 }
                    },
                    new()
                    {
                        AccountId = 2,
                        Description = "بند دائن",
                        Credit = new TemplateValue { Type = TemplateValueType.Fixed, FixedValue = 1000 }
                    }
                },
                DefaultContext = new Dictionary<string, string>
                {
                    ["period"] = "monthly"
                }
            }, _jsonOptions);

            var viewModel = new CompoundJournalDefinitionFormViewModel
            {
                TemplateJson = sampleTemplate,
                TriggerType = CompoundJournalTriggerType.Manual,
                IsActive = true
            };

            await PopulateTemplateLookupsAsync(viewModel);

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "journal.create")]
        public async Task<IActionResult> Create(CompoundJournalDefinitionFormViewModel viewModel)
        {
            ValidateRecurringSettings(viewModel);
            await ValidateTemplateAsync(viewModel.TemplateJson);
            if (!ModelState.IsValid)
            {
                await PopulateTemplateLookupsAsync(viewModel);
                return View(viewModel);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                ModelState.AddModelError(string.Empty, "تعذر تحديد المستخدم الحالي");
                return View(viewModel);
            }

            var definition = new CompoundJournalDefinition
            {
                Name = viewModel.Name,
                Description = viewModel.Description,
                TemplateJson = viewModel.TemplateJson,
                TriggerType = viewModel.TriggerType,
                IsActive = viewModel.IsActive,
                StartDateUtc = viewModel.StartDateUtc,
                EndDateUtc = viewModel.EndDateUtc,
                NextRunUtc = NormalizeNextRun(viewModel),
                Recurrence = viewModel.TriggerType == CompoundJournalTriggerType.Recurring ? viewModel.Recurrence : null,
                RecurrenceInterval = viewModel.TriggerType == CompoundJournalTriggerType.Recurring ? (viewModel.RecurrenceInterval ?? 1) : null,
                CreatedById = userId,
                CreatedAtUtc = DateTime.UtcNow
            };

            _context.CompoundJournalDefinitions.Add(definition);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم إنشاء تعريف القيد المركب بنجاح";
            return RedirectToAction(nameof(Details), new { id = definition.Id });
        }

        [Authorize(Policy = "journal.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var definition = await _context.CompoundJournalDefinitions.FindAsync(id);
            if (definition == null)
            {
                return NotFound();
            }

            var viewModel = new CompoundJournalDefinitionFormViewModel
            {
                Id = definition.Id,
                Name = definition.Name,
                Description = definition.Description,
                TemplateJson = definition.TemplateJson,
                TriggerType = definition.TriggerType,
                IsActive = definition.IsActive,
                StartDateUtc = definition.StartDateUtc,
                EndDateUtc = definition.EndDateUtc,
                NextRunUtc = definition.NextRunUtc,
                Recurrence = definition.Recurrence,
                RecurrenceInterval = definition.RecurrenceInterval
            };

            await PopulateTemplateLookupsAsync(viewModel);

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "journal.edit")]
        public async Task<IActionResult> Edit(int id, CompoundJournalDefinitionFormViewModel viewModel)
        {
            if (id != viewModel.Id)
            {
                return BadRequest();
            }

            ValidateRecurringSettings(viewModel);
            await ValidateTemplateAsync(viewModel.TemplateJson);
            if (!ModelState.IsValid)
            {
                await PopulateTemplateLookupsAsync(viewModel);
                return View(viewModel);
            }

            var definition = await _context.CompoundJournalDefinitions.FindAsync(id);
            if (definition == null)
            {
                return NotFound();
            }

            definition.Name = viewModel.Name;
            definition.Description = viewModel.Description;
            definition.TemplateJson = viewModel.TemplateJson;
            definition.TriggerType = viewModel.TriggerType;
            definition.IsActive = viewModel.IsActive;
            definition.StartDateUtc = viewModel.StartDateUtc;
            definition.EndDateUtc = viewModel.EndDateUtc;
            definition.Recurrence = viewModel.TriggerType == CompoundJournalTriggerType.Recurring ? viewModel.Recurrence : null;
            definition.RecurrenceInterval = viewModel.TriggerType == CompoundJournalTriggerType.Recurring ? (viewModel.RecurrenceInterval ?? 1) : null;
            definition.NextRunUtc = NormalizeNextRun(viewModel);

            await _context.SaveChangesAsync();

            TempData["Success"] = "تم تحديث تعريف القيد المركب";
            return RedirectToAction(nameof(Details), new { id = definition.Id });
        }

        [Authorize(Policy = "journal.delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var definition = await _context.CompoundJournalDefinitions.FindAsync(id);
            if (definition == null)
            {
                return NotFound();
            }

            return View(definition);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "journal.delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var definition = await _context.CompoundJournalDefinitions.FindAsync(id);
            if (definition == null)
            {
                return NotFound();
            }

            _context.CompoundJournalDefinitions.Remove(definition);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حذف تعريف القيد المركب";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "journal.create")]
        public async Task<IActionResult> Execute(int id)
        {
            var definition = await _context.CompoundJournalDefinitions.FindAsync(id);
            if (definition == null)
            {
                return NotFound();
            }

            var template = await _compoundJournalService.ParseTemplateAsync(definition.TemplateJson);

            var branches = await _context.Branches
                .OrderBy(b => b.NameAr)
                .Select(b => new { b.Id, b.NameAr })
                .ToDictionaryAsync(b => b.Id, b => b.NameAr);

            var viewModel = new CompoundJournalExecutionViewModel
            {
                DefinitionId = definition.Id,
                DefinitionName = definition.Name,
                ExecutionDate = DateTime.UtcNow,
                BranchIdOverride = template.BranchId,
                StatusOverride = template.Status,
                ContextJson = template.DefaultContext != null ? JsonSerializer.Serialize(template.DefaultContext, _jsonOptions) : null,
                Branches = branches
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "journal.create")]
        public async Task<IActionResult> Execute(int id, CompoundJournalExecutionViewModel viewModel)
        {
            if (id != viewModel.DefinitionId)
            {
                return BadRequest();
            }

            var definition = await _context.CompoundJournalDefinitions.FindAsync(id);
            if (definition == null)
            {
                return NotFound();
            }

            Dictionary<string, string>? contextOverrides = null;
            if (!string.IsNullOrWhiteSpace(viewModel.ContextJson))
            {
                try
                {
                    contextOverrides = JsonSerializer.Deserialize<Dictionary<string, string>>(viewModel.ContextJson, _jsonOptions);
                }
                catch (JsonException ex)
                {
                    ModelState.AddModelError(nameof(viewModel.ContextJson), $"خطأ في قراءة بيانات السياق: {ex.Message}");
                }
            }

            if (!ModelState.IsValid)
            {
                await PopulateBranchesAsync(viewModel);
                return View(viewModel);
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrWhiteSpace(userId))
            {
                ModelState.AddModelError(string.Empty, "تعذر تحديد المستخدم الحالي");
                await PopulateBranchesAsync(viewModel);
                return View(viewModel);
            }

            try
            {
                var result = await _compoundJournalService.ExecuteAsync(id, new CompoundJournalExecutionRequest
                {
                    UserId = userId,
                    ExecutionDate = DateTime.UtcNow,
                    JournalDate = viewModel.JournalDate,
                    BranchIdOverride = viewModel.BranchIdOverride,
                    DescriptionOverride = viewModel.DescriptionOverride,
                    ReferenceOverride = viewModel.ReferenceOverride,
                    StatusOverride = viewModel.StatusOverride,
                    ContextOverrides = contextOverrides,
                    IsAutomatic = false
                });

                if (result.Success)
                {
                    TempData["Success"] = "تم تنفيذ القيد المركب بنجاح";
                }
                else
                {
                    TempData["Warning"] = result.Message ?? "لم يتم تنفيذ القيد";
                }

                return RedirectToAction(nameof(Details), new { id });
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(string.Empty, ex.Message);
                await PopulateBranchesAsync(viewModel);
                return View(viewModel);
            }
        }

        private async Task ValidateTemplateAsync(string templateJson)
        {
            try
            {
                await _compoundJournalService.ParseTemplateAsync(templateJson);
            }
            catch (Exception ex)
            {
                ModelState.AddModelError(nameof(CompoundJournalDefinitionFormViewModel.TemplateJson), ex.Message);
            }
        }

        private async Task PopulateTemplateLookupsAsync(CompoundJournalDefinitionFormViewModel viewModel)
        {
            viewModel.Branches = await _context.Branches
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(b.NameAr)
                        ? (string.IsNullOrWhiteSpace(b.NameEn) ? b.Code : b.NameEn)
                        : b.NameAr
                })
                .ToListAsync();

            viewModel.Accounts = await _context.Accounts
                .OrderBy(a => a.Code)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(a.Code)
                        ? (string.IsNullOrWhiteSpace(a.NameAr) ? a.NameEn ?? $"حساب #{a.Id}" : a.NameAr)
                        : $"{a.Code} - {(string.IsNullOrWhiteSpace(a.NameAr) ? a.NameEn ?? $"حساب #{a.Id}" : a.NameAr)}"
                })
                .ToListAsync();

            viewModel.CostCenters = await _context.CostCenters
                .OrderBy(c => c.NameAr)
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(c.NameAr)
                        ? (string.IsNullOrWhiteSpace(c.NameEn) ? c.Code : c.NameEn)
                        : c.NameAr
                })
                .ToListAsync();

            viewModel.JournalStatuses = Enum.GetValues<JournalEntryStatus>()
                .Select(status => new SelectListItem
                {
                    Value = status.ToString(),
                    Text = GetJournalStatusDisplay(status)
                })
                .ToList();
        }

        private static string GetJournalStatusDisplay(JournalEntryStatus status)
        {
            return status switch
            {
                JournalEntryStatus.Draft => "مسودة",
                JournalEntryStatus.Posted => "مرحَّل",
                JournalEntryStatus.Approved => "معتمد",
                JournalEntryStatus.Cancelled => "ملغى",
                _ => status.ToString()
            };
        }

        private void ValidateRecurringSettings(CompoundJournalDefinitionFormViewModel viewModel)
        {
            if (viewModel.TriggerType == CompoundJournalTriggerType.Recurring)
            {
                if (viewModel.Recurrence == null)
                {
                    ModelState.AddModelError(nameof(viewModel.Recurrence), "يجب تحديد نوع التكرار");
                }

                if (viewModel.RecurrenceInterval == null || viewModel.RecurrenceInterval < 1)
                {
                    ModelState.AddModelError(nameof(viewModel.RecurrenceInterval), "يجب أن تكون فترة التكرار رقمًا صحيحًا أكبر من الصفر");
                }
            }
        }

        private static DateTime? NormalizeNextRun(CompoundJournalDefinitionFormViewModel viewModel)
        {
            if (viewModel.TriggerType == CompoundJournalTriggerType.Manual)
            {
                return null;
            }

            if (viewModel.TriggerType == CompoundJournalTriggerType.OneTime)
            {
                return viewModel.NextRunUtc ?? viewModel.StartDateUtc;
            }

            if (viewModel.TriggerType == CompoundJournalTriggerType.Recurring)
            {
                return viewModel.NextRunUtc ?? viewModel.StartDateUtc ?? DateTime.UtcNow.AddMinutes(5);
            }

            return viewModel.NextRunUtc;
        }

        private async Task PopulateBranchesAsync(CompoundJournalExecutionViewModel viewModel)
        {
            viewModel.Branches = await _context.Branches
                .OrderBy(b => b.NameAr)
                .Select(b => new { b.Id, b.NameAr })
                .ToDictionaryAsync(b => b.Id, b => b.NameAr);
        }
    }
}
