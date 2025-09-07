using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "currencies.view")]
    public class CurrenciesController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CurrenciesController> _logger;

        public CurrenciesController(ApplicationDbContext context, ILogger<CurrenciesController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var currencies = await _context.Currencies
                .OrderBy(c => c.Code)
                .ToListAsync();

            var viewModels = currencies.Select(c => new CurrencyViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Code = c.Code,
                ExchangeRate = c.ExchangeRate,
                IsBase = c.IsBase
            }).ToList();

            return View(viewModels);
        }

        [Authorize(Policy = "currencies.create")]
        public IActionResult Create()
        {
            return View(new CreateCurrencyViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "currencies.create")]
        public async Task<IActionResult> Create(CreateCurrencyViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (await _context.Currencies.AnyAsync(c => c.Code == model.Code))
                {
                    ModelState.AddModelError("Code", "كود العملة موجود مسبقاً");
                    return View(model);
                }

                if (model.IsBase)
                {
                    var existing = await _context.Currencies.Where(c => c.IsBase).ToListAsync();
                    foreach (var cur in existing)
                        cur.IsBase = false;
                }

                var currency = new Currency
                {
                    Name = model.Name,
                    Code = model.Code,
                    ExchangeRate = model.ExchangeRate,
                    IsBase = model.IsBase
                };

                _context.Currencies.Add(currency);
                await _context.SaveChangesAsync();
                _logger.LogInformation("Currency {Code} created successfully", model.Code);
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [Authorize(Policy = "currencies.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var currency = await _context.Currencies.FindAsync(id);
            if (currency == null)
                return NotFound();

            var model = new EditCurrencyViewModel
            {
                Id = currency.Id,
                Name = currency.Name,
                Code = currency.Code,
                ExchangeRate = currency.ExchangeRate,
                IsBase = currency.IsBase
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "currencies.edit")]
        public async Task<IActionResult> Edit(EditCurrencyViewModel model)
        {
            if (ModelState.IsValid)
            {
                if (await _context.Currencies.AnyAsync(c => c.Code == model.Code && c.Id != model.Id))
                {
                    ModelState.AddModelError("Code", "كود العملة موجود مسبقاً");
                    return View(model);
                }

                var currency = await _context.Currencies.FindAsync(model.Id);
                if (currency == null)
                    return NotFound();

                if (model.IsBase)
                {
                    var existing = await _context.Currencies.Where(c => c.IsBase && c.Id != model.Id).ToListAsync();
                    foreach (var cur in existing)
                        cur.IsBase = false;
                }

                currency.Name = model.Name;
                currency.Code = model.Code;
                currency.ExchangeRate = model.ExchangeRate;
                currency.IsBase = model.IsBase;

                await _context.SaveChangesAsync();
                _logger.LogInformation("Currency {Code} updated successfully", model.Code);
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "currencies.delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var currency = await _context.Currencies
                .Include(c => c.Accounts)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (currency == null)
                return NotFound();

            if (currency.IsBase || currency.Accounts.Any())
            {
                TempData["ErrorMessage"] = "لا يمكن حذف هذه العملة";
                return RedirectToAction(nameof(Index));
            }

            _context.Currencies.Remove(currency);
            await _context.SaveChangesAsync();
            _logger.LogInformation("Currency {Code} deleted successfully", currency.Code);
            TempData["SuccessMessage"] = "تم حذف العملة بنجاح";
            return RedirectToAction(nameof(Index));
        }
    }
}
