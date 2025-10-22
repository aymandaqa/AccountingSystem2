using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using System.Linq;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "costcenters.view")]
    public class CostCentersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ILogger<CostCentersController> _logger;

        public CostCentersController(ApplicationDbContext context, ILogger<CostCentersController> logger)
        {
            _context = context;
            _logger = logger;
        }

        public async Task<IActionResult> Index()
        {
            var costCenters = await _context.CostCenters
                .Include(cc => cc.JournalEntryLines)
                    .ThenInclude(line => line.JournalEntry)
                .OrderBy(cc => cc.Code)
                .ToListAsync();

            var viewModels = costCenters.Select(cc => new CostCenterViewModel
            {
                Id = cc.Id,
                Code = cc.Code,
                NameAr = cc.NameAr,
                NameEn = cc.NameEn,
                Description = cc.Description,
                IsActive = cc.IsActive,
                CreatedAt = cc.CreatedAt,
                TransactionCount = cc.JournalEntryLines.Count(line => line.JournalEntry.Status != JournalEntryStatus.Cancelled)
            }).ToList();

            return View(viewModels);
        }

        public async Task<IActionResult> Details(int id)
        {
            var costCenter = await _context.CostCenters
                .Include(cc => cc.JournalEntryLines)
                    .ThenInclude(jel => jel.JournalEntry)
                .Include(cc => cc.JournalEntryLines)
                    .ThenInclude(jel => jel.Account)
                .FirstOrDefaultAsync(cc => cc.Id == id);

            if (costCenter == null)
            {
                return NotFound();
            }

            var viewModel = new CostCenterDetailsViewModel
            {
                Id = costCenter.Id,
                Code = costCenter.Code,
                NameAr = costCenter.NameAr,
                NameEn = costCenter.NameEn,
                Description = costCenter.Description,
                IsActive = costCenter.IsActive,
                CreatedAt = costCenter.CreatedAt,
                UpdatedAt = costCenter.UpdatedAt,
                TotalDebit = costCenter.JournalEntryLines
                    .Where(jel => jel.JournalEntry.Status != JournalEntryStatus.Cancelled)
                    .Sum(jel => jel.DebitAmount),
                TotalCredit = costCenter.JournalEntryLines
                    .Where(jel => jel.JournalEntry.Status != JournalEntryStatus.Cancelled)
                    .Sum(jel => jel.CreditAmount),
                TransactionCount = costCenter.JournalEntryLines.Count(jel => jel.JournalEntry.Status != JournalEntryStatus.Cancelled),
                RecentTransactions = costCenter.JournalEntryLines
                    .Where(jel => jel.JournalEntry.Status != JournalEntryStatus.Cancelled)
                    .OrderByDescending(jel => jel.JournalEntry.Date)
                    .Take(10)
                    .Select(jel => new CostCenterTransactionViewModel
                    {
                        Date = jel.JournalEntry.Date,
                        Reference = jel.JournalEntry.Number,
                        AccountName = jel.Account.NameAr,
                        Description = jel.Description ?? string.Empty,
                        DebitAmount = jel.DebitAmount,
                        CreditAmount = jel.CreditAmount
                    }).ToList()
            };

            return View(viewModel);
        }

        [Authorize(Policy = "costcenters.create")]
        public IActionResult Create()
        {
            return View(new CreateCostCenterViewModel());
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "costcenters.create")]
        public async Task<IActionResult> Create(CreateCostCenterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if code already exists
                if (await _context.CostCenters.AnyAsync(cc => cc.Code == model.Code))
                {
                    ModelState.AddModelError("Code", "كود مركز التكلفة موجود مسبقاً");
                    return View(model);
                }

                var costCenter = new CostCenter
                {
                    Code = model.Code,
                    NameAr = model.NameAr,
                    NameEn = model.NameEn,
                    Description = model.Description,
                    IsActive = model.IsActive
                };

                _context.CostCenters.Add(costCenter);
                await _context.SaveChangesAsync();

                _logger.LogInformation("Cost center {Code} created successfully.", model.Code);
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [Authorize(Policy = "costcenters.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var costCenter = await _context.CostCenters.FindAsync(id);
            if (costCenter == null)
            {
                return NotFound();
            }

            var viewModel = new EditCostCenterViewModel
            {
                Id = costCenter.Id,
                Code = costCenter.Code,
                NameAr = costCenter.NameAr,
                NameEn = costCenter.NameEn,
                Description = costCenter.Description,
                IsActive = costCenter.IsActive
            };

            return View(viewModel);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "costcenters.edit")]
        public async Task<IActionResult> Edit(EditCostCenterViewModel model)
        {
            if (ModelState.IsValid)
            {
                // Check if code already exists for other cost centers
                if (await _context.CostCenters.AnyAsync(cc => cc.Code == model.Code && cc.Id != model.Id))
                {
                    ModelState.AddModelError("Code", "كود مركز التكلفة موجود مسبقاً");
                    return View(model);
                }

                var costCenter = await _context.CostCenters.FindAsync(model.Id);
                if (costCenter == null)
                {
                    return NotFound();
                }

                costCenter.Code = model.Code;
                costCenter.NameAr = model.NameAr;
                costCenter.NameEn = model.NameEn;
                costCenter.Description = model.Description;
                costCenter.IsActive = model.IsActive;
                costCenter.UpdatedAt = DateTime.Now;

                await _context.SaveChangesAsync();

                _logger.LogInformation("Cost center {Code} updated successfully.", model.Code);
                return RedirectToAction(nameof(Index));
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "costcenters.delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var costCenter = await _context.CostCenters.FindAsync(id);

            if (costCenter == null)
            {
                return NotFound();
            }

            // Check if cost center has related transactions
            var hasActiveTransactions = await _context.JournalEntryLines
                .Where(line => line.CostCenterId == id)
                .AnyAsync(line => line.JournalEntry != null && line.JournalEntry.Status != JournalEntryStatus.Cancelled);

            if (hasActiveTransactions)
            {
                TempData["Error"] = "لا يمكن حذف مركز التكلفة لوجود معاملات مرتبطة به";
                return RedirectToAction(nameof(Index));
            }

            _context.CostCenters.Remove(costCenter);
            await _context.SaveChangesAsync();

            _logger.LogInformation("Cost center {Code} deleted successfully.", costCenter.Code);
            TempData["Success"] = "تم حذف مركز التكلفة بنجاح";
            return RedirectToAction(nameof(Index));
        }

        [HttpGet]
        public async Task<IActionResult> GetCostCenterReport(int id, DateTime? fromDate, DateTime? toDate)
        {
            var costCenter = await _context.CostCenters
                .Include(cc => cc.JournalEntryLines)
                    .ThenInclude(jel => jel.JournalEntry)
                .Include(cc => cc.JournalEntryLines)
                    .ThenInclude(jel => jel.Account)
                .FirstOrDefaultAsync(cc => cc.Id == id);

            if (costCenter == null)
            {
                return NotFound();
            }

            var query = costCenter.JournalEntryLines
                .Where(jel => jel.JournalEntry.Status != JournalEntryStatus.Cancelled)
                .AsQueryable();

            if (fromDate.HasValue)
            {
                query = query.Where(jel => jel.JournalEntry.Date >= fromDate.Value);
            }

            if (toDate.HasValue)
            {
                query = query.Where(jel => jel.JournalEntry.Date <= toDate.Value);
            }

            var transactions = query
                .OrderByDescending(jel => jel.JournalEntry.Date)
                .Select(jel => new CostCenterTransactionViewModel
                {
                    Date = jel.JournalEntry.Date,
                    Reference = jel.JournalEntry.Number,
                    AccountName = jel.Account.NameAr,
                    Description = jel.Description ?? string.Empty,
                    DebitAmount = jel.DebitAmount,
                    CreditAmount = jel.CreditAmount
                }).ToList();

            var reportViewModel = new CostCenterReportViewModel
            {
                CostCenter = costCenter,
                FromDate = fromDate ?? DateTime.Now.AddMonths(-1),
                ToDate = toDate ?? DateTime.Now,
                Transactions = transactions,
                TotalDebit = transactions.Sum(t => t.DebitAmount),
                TotalCredit = transactions.Sum(t => t.CreditAmount)
            };

            return View("Report", reportViewModel);
        }
    }
}

