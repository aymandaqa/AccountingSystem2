using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "suppliertypes.view")]
    public class SupplierTypesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private const string StatusMessageKey = "StatusMessage";

        public SupplierTypesController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var items = await _context.SupplierTypes
                .AsNoTracking()
                .OrderBy(t => t.Name)
                .Select(t => new SupplierTypeListItemViewModel
                {
                    Id = t.Id,
                    Name = t.Name,
                    IsActive = t.IsActive,
                    SuppliersCount = t.Suppliers.Count
                })
                .ToListAsync();

            ViewBag.StatusMessage = TempData[StatusMessageKey]?.ToString();
            return View(items);
        }

        [Authorize(Policy = "suppliertypes.create")]
        public IActionResult Create()
        {
            var model = new SupplierTypeFormViewModel();
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "suppliertypes.create")]
        public async Task<IActionResult> Create(SupplierTypeFormViewModel model)
        {
            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Name = model.Name.Trim();

            var exists = await _context.SupplierTypes
                .AnyAsync(t => t.Name == model.Name);
            if (exists)
            {
                ModelState.AddModelError(nameof(model.Name), "اسم نوع المورد مستخدم من قبل.");
                return View(model);
            }

            var supplierType = new SupplierType
            {
                Name = model.Name,
                IsActive = model.IsActive
            };

            _context.SupplierTypes.Add(supplierType);
            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم إنشاء نوع المورد بنجاح.";
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "suppliertypes.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var supplierType = await _context.SupplierTypes
                .AsNoTracking()
                .FirstOrDefaultAsync(t => t.Id == id);

            if (supplierType == null)
            {
                return NotFound();
            }

            var model = new SupplierTypeFormViewModel
            {
                Id = supplierType.Id,
                Name = supplierType.Name,
                IsActive = supplierType.IsActive
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "suppliertypes.edit")]
        public async Task<IActionResult> Edit(int id, SupplierTypeFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (!ModelState.IsValid)
            {
                return View(model);
            }

            model.Name = model.Name.Trim();

            var exists = await _context.SupplierTypes
                .AnyAsync(t => t.Id != id && t.Name == model.Name);
            if (exists)
            {
                ModelState.AddModelError(nameof(model.Name), "اسم نوع المورد مستخدم من قبل.");
                return View(model);
            }

            var supplierType = await _context.SupplierTypes
                .FirstOrDefaultAsync(t => t.Id == id);
            if (supplierType == null)
            {
                return NotFound();
            }

            supplierType.Name = model.Name;
            supplierType.IsActive = model.IsActive;

            await _context.SaveChangesAsync();

            TempData[StatusMessageKey] = "تم تحديث نوع المورد بنجاح.";
            return RedirectToAction(nameof(Index));
        }
    }
}
