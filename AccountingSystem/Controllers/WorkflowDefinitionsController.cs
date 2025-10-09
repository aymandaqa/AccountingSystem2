using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.Workflows;
using AccountingSystem.ViewModels.Workflows;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Text.Json;
using System.Threading.Tasks;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "workflowdefinitions.manage")]
    public class WorkflowDefinitionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public WorkflowDefinitionsController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        public async Task<IActionResult> Index()
        {
            var definitions = await _context.WorkflowDefinitions
                .Include(d => d.Branch)
                .Include(d => d.Steps)
                .Where(d => d.DocumentType == WorkflowDocumentType.PaymentVoucher)
                .OrderByDescending(d => d.IsActive)
                .ThenBy(d => d.Name)
                .ToListAsync();
            return View(definitions);
        }

        public async Task<IActionResult> Create()
        {
            await PopulateLookupsAsync();
            var model = new WorkflowDefinitionViewModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(WorkflowDefinitionViewModel model, string stepsJson)
        {
            if (!TryParseSteps(stepsJson, model))
            {
                ModelState.AddModelError(string.Empty, "يجب إضافة خطوة واحدة على الأقل");
            }

            if (!ModelState.IsValid)
            {
                await PopulateLookupsAsync();
                return View(model);
            }

            var user = await _userManager.GetUserAsync(User);
            var definition = new WorkflowDefinition
            {
                Name = model.Name,
                DocumentType = model.DocumentType,
                BranchId = model.BranchId,
                CreatedById = user?.Id,
                CreatedAt = DateTime.UtcNow,
                IsActive = true
            };

            _context.WorkflowDefinitions.Add(definition);
            await _context.SaveChangesAsync();

            await SaveStepsAsync(definition, model.Steps);

            return RedirectToAction(nameof(Index));
        }

        public async Task<IActionResult> Edit(int id)
        {
            var definition = await _context.WorkflowDefinitions
                .Include(d => d.Steps)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (definition == null)
            {
                return NotFound();
            }

            var model = new WorkflowDefinitionViewModel
            {
                Id = definition.Id,
                Name = definition.Name,
                BranchId = definition.BranchId,
                DocumentType = definition.DocumentType,
                Steps = definition.Steps
                    .OrderBy(s => s.Order)
                    .Select(s => new WorkflowStepInputModel
                    {
                        Id = s.Id,
                        Order = s.Order,
                        StepType = s.StepType,
                        ApproverUserId = s.ApproverUserId,
                        BranchId = s.BranchId,
                        RequiredPermission = s.RequiredPermission
                    }).ToList()
            };

            await PopulateLookupsAsync();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Edit(int id, WorkflowDefinitionViewModel model, string stepsJson)
        {
            var definition = await _context.WorkflowDefinitions
                .Include(d => d.Steps)
                .FirstOrDefaultAsync(d => d.Id == id);
            if (definition == null)
            {
                return NotFound();
            }

            if (!TryParseSteps(stepsJson, model))
            {
                ModelState.AddModelError(string.Empty, "يجب إضافة خطوة واحدة على الأقل");
            }

            if (!ModelState.IsValid)
            {
                await PopulateLookupsAsync();
                return View(model);
            }

            definition.Name = model.Name;
            definition.BranchId = model.BranchId;
            definition.DocumentType = model.DocumentType;
            definition.UpdatedAt = DateTime.UtcNow;

            var stepIds = definition.Steps.Select(s => s.Id).ToList();
            if (stepIds.Count > 0)
            {
                var relatedActions = await _context.WorkflowActions
                    .Where(a => stepIds.Contains(a.WorkflowStepId))
                    .ToListAsync();

                if (relatedActions.Count > 0)
                {
                    _context.WorkflowActions.RemoveRange(relatedActions);
                }

                _context.WorkflowSteps.RemoveRange(definition.Steps);
                await _context.SaveChangesAsync();
            }

            await SaveStepsAsync(definition, model.Steps);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ToggleStatus(int id)
        {
            var definition = await _context.WorkflowDefinitions.FindAsync(id);
            if (definition == null)
            {
                return NotFound();
            }

            definition.IsActive = !definition.IsActive;
            definition.UpdatedAt = DateTime.UtcNow;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private async Task PopulateLookupsAsync()
        {
            ViewBag.Users = await _context.Users
                .Where(u => u.IsActive)
                .Select(u => new { u.Id, Name = (u.FirstName + " " + u.LastName).Trim() })
                .OrderBy(u => u.Name)
                .ToListAsync();

            ViewBag.Permissions = await _context.Permissions
                .OrderBy(p => p.DisplayName)
                .Select(p => new { p.Name, p.DisplayName })
                .ToListAsync();

            ViewBag.Branches = await _context.Branches
                .OrderBy(b => b.NameAr)
                .Select(b => new { b.Id, Name = b.NameAr })
                .ToListAsync();
        }

        private bool TryParseSteps(string stepsJson, WorkflowDefinitionViewModel model)
        {
            if (string.IsNullOrWhiteSpace(stepsJson))
            {
                return false;
            }

            try
            {
                var steps = JsonSerializer.Deserialize<List<WorkflowStepInputModel>>(stepsJson) ?? new List<WorkflowStepInputModel>();
                model.Steps = steps.Where(s => s.StepType != 0).OrderBy(s => s.Order).ToList();
                return model.Steps.Count > 0;
            }
            catch
            {
                return false;
            }
        }

        private async Task SaveStepsAsync(WorkflowDefinition definition, List<WorkflowStepInputModel> steps)
        {
            var ordered = steps.OrderBy(s => s.Order).ToList();
            var workflowSteps = ordered.Select((step, index) => new WorkflowStep
            {
                WorkflowDefinitionId = definition.Id,
                Order = index + 1,
                StepType = step.StepType,
                ApproverUserId = step.StepType == WorkflowStepType.SpecificUser ? step.ApproverUserId : null,
                BranchId = step.StepType == WorkflowStepType.Branch ? step.BranchId : null,
                RequiredPermission = step.StepType == WorkflowStepType.Permission ? step.RequiredPermission : null
            }).ToList();

            await _context.WorkflowSteps.AddRangeAsync(workflowSteps);
            await _context.SaveChangesAsync();
        }
    }
}
