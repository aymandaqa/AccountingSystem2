using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using System;
using System.Collections.Generic;

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
                .Include(c => c.Units)
                .OrderBy(c => c.Code)
                .ToListAsync();

            var viewModels = currencies.Select(c => new CurrencyViewModel
            {
                Id = c.Id,
                Name = c.Name,
                Code = c.Code,
                ExchangeRate = c.ExchangeRate,
                IsBase = c.IsBase,
                Units = c.Units
                    .OrderBy(u => u.ValueInBaseUnit)
                    .Select(u => new CurrencyUnitInputModel
                    {
                        Id = u.Id,
                        Name = u.Name,
                        ValueInBaseUnit = u.ValueInBaseUnit
                    }).ToList()
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
            model.Units = model.Units?
                .Where(u => !string.IsNullOrWhiteSpace(u.Name))
                .Select(u =>
                {
                    u.Name = u.Name.Trim();
                    return u;
                })
                .ToList() ?? new List<CurrencyUnitInputModel>();

            if (!model.Units.Any())
                ModelState.AddModelError("Units", "يجب إضافة وحدة واحدة على الأقل.");

            if (!model.Units.Any(u => Math.Abs(u.ValueInBaseUnit - 1m) < 0.000001m))
                ModelState.AddModelError("Units", "يجب تحديد وحدة أساسية بقيمة 1.");

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

                foreach (var unit in model.Units)
                {
                    _context.CurrencyUnits.Add(new CurrencyUnit
                    {
                        CurrencyId = currency.Id,
                        Name = unit.Name,
                        ValueInBaseUnit = unit.ValueInBaseUnit
                    });
                }

                await _context.SaveChangesAsync();
                _logger.LogInformation("Currency {Code} created successfully", model.Code);
                return RedirectToAction(nameof(Index));
            }
            return View(model);
        }

        [Authorize(Policy = "currencies.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var currency = await _context.Currencies
                .Include(c => c.Units)
                .FirstOrDefaultAsync(c => c.Id == id);
            if (currency == null)
                return NotFound();

            var model = new EditCurrencyViewModel
            {
                Id = currency.Id,
                Name = currency.Name,
                Code = currency.Code,
                ExchangeRate = currency.ExchangeRate,
                IsBase = currency.IsBase,
                Units = currency.Units
                    .OrderBy(u => u.ValueInBaseUnit)
                    .Select(u => new CurrencyUnitInputModel
                    {
                        Id = u.Id,
                        Name = u.Name,
                        ValueInBaseUnit = u.ValueInBaseUnit
                    }).ToList()
            };

            if (!model.Units.Any())
            {
                model.Units.Add(new CurrencyUnitInputModel
                {
                    ValueInBaseUnit = 1m
                });
            }
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "currencies.edit")]
        public async Task<IActionResult> Edit(EditCurrencyViewModel model)
        {
            model.Units = model.Units?
                .Where(u => !string.IsNullOrWhiteSpace(u.Name))
                .Select(u =>
                {
                    u.Name = u.Name.Trim();
                    return u;
                })
                .ToList() ?? new List<CurrencyUnitInputModel>();

            if (!model.Units.Any())
                ModelState.AddModelError("Units", "يجب إضافة وحدة واحدة على الأقل.");

            if (!model.Units.Any(u => Math.Abs(u.ValueInBaseUnit - 1m) < 0.000001m))
                ModelState.AddModelError("Units", "يجب تحديد وحدة أساسية بقيمة 1.");

            if (ModelState.IsValid)
            {
                if (await _context.Currencies.AnyAsync(c => c.Code == model.Code && c.Id != model.Id))
                {
                    ModelState.AddModelError("Code", "كود العملة موجود مسبقاً");
                    return View(model);
                }

                var currency = await _context.Currencies
                    .Include(c => c.Units)
                    .FirstOrDefaultAsync(c => c.Id == model.Id);
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

                var existingUnits = currency.Units.ToDictionary(u => u.Id);

                // Remove deleted units
                var postedUnitIds = model.Units.Where(u => u.Id.HasValue).Select(u => u.Id!.Value).ToHashSet();
                var unitsToRemove = currency.Units.Where(u => !postedUnitIds.Contains(u.Id)).ToList();
                if (unitsToRemove.Any())
                    _context.CurrencyUnits.RemoveRange(unitsToRemove);

                foreach (var unitModel in model.Units)
                {
                    if (unitModel.Id.HasValue && existingUnits.TryGetValue(unitModel.Id.Value, out var existingUnit))
                    {
                        existingUnit.Name = unitModel.Name;
                        existingUnit.ValueInBaseUnit = unitModel.ValueInBaseUnit;
                    }
                    else
                    {
                        _context.CurrencyUnits.Add(new CurrencyUnit
                        {
                            CurrencyId = currency.Id,
                            Name = unitModel.Name,
                            ValueInBaseUnit = unitModel.ValueInBaseUnit
                        });
                    }
                }

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
