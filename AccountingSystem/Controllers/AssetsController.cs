using System;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "assets.view")]
    public class AssetsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public AssetsController(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            var assets = await _context.Assets
                .Include(a => a.Branch)
                .OrderBy(a => a.Name)
                .ToListAsync();

            var model = assets.Select(a => new AssetListViewModel
            {
                Id = a.Id,
                Name = a.Name,
                Type = a.Type,
                BranchName = a.Branch.NameAr,
                AssetNumber = a.AssetNumber,
                Notes = a.Notes,
                CreatedAt = a.CreatedAt,
                UpdatedAt = a.UpdatedAt
            }).ToList();

            return View(model);
        }

        [Authorize(Policy = "assets.create")]
        public async Task<IActionResult> Create()
        {
            var model = new AssetFormViewModel
            {
                Branches = await GetBranchesAsync()
            };
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assets.create")]
        public async Task<IActionResult> Create(AssetFormViewModel model)
        {
            if (ModelState.IsValid)
            {
                var asset = new Asset
                {
                    Name = model.Name,
                    Type = model.Type,
                    BranchId = model.BranchId,
                    AssetNumber = model.AssetNumber,
                    Notes = model.Notes
                };

                _context.Assets.Add(asset);
                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            model.Branches = await GetBranchesAsync();
            return View(model);
        }

        [Authorize(Policy = "assets.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var asset = await _context.Assets.FindAsync(id);
            if (asset == null)
            {
                return NotFound();
            }

            var model = new AssetFormViewModel
            {
                Id = asset.Id,
                Name = asset.Name,
                Type = asset.Type,
                BranchId = asset.BranchId,
                AssetNumber = asset.AssetNumber,
                Notes = asset.Notes,
                Branches = await GetBranchesAsync()
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assets.edit")]
        public async Task<IActionResult> Edit(int id, AssetFormViewModel model)
        {
            if (id != model.Id)
            {
                return NotFound();
            }

            if (ModelState.IsValid)
            {
                var asset = await _context.Assets.FindAsync(id);
                if (asset == null)
                {
                    return NotFound();
                }

                asset.Name = model.Name;
                asset.Type = model.Type;
                asset.BranchId = model.BranchId;
                asset.AssetNumber = model.AssetNumber;
                asset.Notes = model.Notes;
                asset.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();
                return RedirectToAction(nameof(Index));
            }

            model.Branches = await GetBranchesAsync();
            return View(model);
        }

        [Authorize(Policy = "assets.delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var asset = await _context.Assets
                .Include(a => a.Branch)
                .FirstOrDefaultAsync(a => a.Id == id);
            if (asset == null)
            {
                return NotFound();
            }

            var model = new AssetListViewModel
            {
                Id = asset.Id,
                Name = asset.Name,
                Type = asset.Type,
                BranchName = asset.Branch.NameAr,
                AssetNumber = asset.AssetNumber,
                Notes = asset.Notes,
                CreatedAt = asset.CreatedAt,
                UpdatedAt = asset.UpdatedAt
            };

            return View(model);
        }

        [HttpPost, ActionName("Delete")]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assets.delete")]
        public async Task<IActionResult> DeleteConfirmed(int id)
        {
            var asset = await _context.Assets.FindAsync(id);
            if (asset == null)
            {
                return NotFound();
            }

            _context.Assets.Remove(asset);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        private async Task<IEnumerable<SelectListItem>> GetBranchesAsync()
        {
            return await _context.Branches
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                }).ToListAsync();
        }
    }
}
