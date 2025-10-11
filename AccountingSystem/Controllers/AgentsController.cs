using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System.Linq;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "agents.view")]
    public class AgentsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly IAccountService _accountService;

        public AgentsController(ApplicationDbContext context, IAccountService accountService)
        {
            _context = context;
            _accountService = accountService;
        }

        public async Task<IActionResult> Index()
        {
            var agents = await _context.Agents
                .Include(a => a.Branch)
                .Include(a => a.Account)
                .OrderBy(a => a.Name)
                .ToListAsync();

            return View(agents);
        }

        [Authorize(Policy = "agents.create")]
        public async Task<IActionResult> Create()
        {
            await PopulateSelectionsAsync();
            return View(new Agent());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "agents.create")]
        public async Task<IActionResult> Create(Agent model)
        {
            if (!await _context.Branches.AnyAsync(b => b.Id == model.BranchId))
            {
                ModelState.AddModelError(nameof(Agent.BranchId), "الفرع المحدد غير موجود.");
            }

            var parentAccountId = await GetAgentsParentAccountIdAsync();
            if (!parentAccountId.HasValue)
            {
                ModelState.AddModelError(string.Empty, "يرجى ضبط حساب الوكلاء الرئيسي في إعدادات النظام قبل إنشاء وكيل.");
            }

            if (ModelState.IsValid && parentAccountId.HasValue)
            {
                var branch = await _context.Branches.FindAsync(model.BranchId);
                var accountName = branch != null ? $"{branch.NameAr} - {model.Name}" : model.Name;

                var (accountId, _) = await _accountService.CreateAccountAsync(accountName, parentAccountId.Value);
                model.AccountId = accountId;

                _context.Agents.Add(model);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            await PopulateSelectionsAsync(model.BranchId);
            return View(model);
        }

        [Authorize(Policy = "agents.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var agent = await _context.Agents.FindAsync(id);
            if (agent == null)
            {
                return NotFound();
            }

            await PopulateSelectionsAsync(agent.BranchId);
            return View(agent);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "agents.edit")]
        public async Task<IActionResult> Edit(int id, Agent model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!await _context.Branches.AnyAsync(b => b.Id == model.BranchId))
            {
                ModelState.AddModelError(nameof(Agent.BranchId), "الفرع المحدد غير موجود.");
            }

            if (!ModelState.IsValid)
            {
                await PopulateSelectionsAsync(model.BranchId);
                return View(model);
            }

            var agent = await _context.Agents
                .Include(a => a.Account)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (agent == null)
            {
                return NotFound();
            }

            agent.Name = model.Name;
            agent.Address = model.Address;
            agent.BranchId = model.BranchId;

            if (agent.Account != null)
            {
                var branch = await _context.Branches.FindAsync(model.BranchId);
                var accountName = branch != null ? $"{branch.NameAr} - {model.Name}" : model.Name;
                agent.Account.NameAr = accountName;
                agent.Account.NameEn = accountName;
            }

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "agents.delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var agent = await _context.Agents
                .Include(a => a.Account)
                    .ThenInclude(a => a.JournalEntryLines)
                .FirstOrDefaultAsync(a => a.Id == id);

            if (agent == null)
            {
                return NotFound();
            }

            if (agent.Account != null && agent.Account.JournalEntryLines.Any())
            {
                TempData["Error"] = "لا يمكن حذف الوكيل لوجود معاملات مرتبطة بالحساب.";
                return RedirectToAction(nameof(Index));
            }

            if (agent.Account != null)
            {
                _context.Accounts.Remove(agent.Account);
            }

            _context.Agents.Remove(agent);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم حذف الوكيل بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        private async Task<int?> GetAgentsParentAccountIdAsync()
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "AgentsParentAccountId");
            if (setting != null && int.TryParse(setting.Value, out var parentId))
            {
                var exists = await _context.Accounts.AnyAsync(a => a.Id == parentId && a.CanHaveChildren);
                if (exists)
                {
                    return parentId;
                }
            }

            return null;
        }

        private async Task PopulateSelectionsAsync(int? selectedBranchId = null)
        {
            ViewBag.Branches = await _context.Branches
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr,
                    Selected = selectedBranchId.HasValue && selectedBranchId.Value == b.Id
                })
                .ToListAsync();
        }
    }
}
