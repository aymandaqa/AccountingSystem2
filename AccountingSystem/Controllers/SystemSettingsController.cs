using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "systemsettings.view")]
    public class SystemSettingsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public SystemSettingsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var settings = await _context.SystemSettings
                .OrderBy(s => s.Key)
                .ToListAsync();
            return View(settings);
        }

        [Authorize(Policy = "systemsettings.create")]
        public IActionResult Create()
        {
            return View(new SystemSetting());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "systemsettings.create")]
        public async Task<IActionResult> Create(SystemSetting model)
        {
            if (ModelState.IsValid)
            {
                _context.SystemSettings.Add(model);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [Authorize(Policy = "systemsettings.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var setting = await _context.SystemSettings.FindAsync(id);
            if (setting == null)
            {
                return NotFound();
            }
            return View(setting);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "systemsettings.edit")]
        public async Task<IActionResult> Edit(int id, SystemSetting model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }
            if (ModelState.IsValid)
            {
                var setting = await _context.SystemSettings.FindAsync(id);
                if (setting == null)
                {
                    return NotFound();
                }
                setting.Key = model.Key;
                setting.Value = model.Value;
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "systemsettings.delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var setting = await _context.SystemSettings.FindAsync(id);
            if (setting == null)
            {
                return NotFound();
            }
            _context.SystemSettings.Remove(setting);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }
    }
}
