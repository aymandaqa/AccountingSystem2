using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "journal.view")]
    public class JournalEntriesController : Controller
    {
        private readonly ApplicationDbContext _context;

        public JournalEntriesController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: JournalEntries
        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> Index()
        {
            var journalEntries = await _context.JournalEntries
                .Include(j => j.Branch)
                .Include(j => j.Lines)
                .OrderByDescending(j => j.Date)
                .ToListAsync();

            var viewModel = new JournalEntriesIndexViewModel
            {
                JournalEntries = journalEntries.Select(j => new JournalEntryViewModel
                {
                    Id = j.Id,
                    Number = j.Number,
                    Date = j.Date,
                    Description = j.Description,
                    Reference = j.Reference,
                    Status = j.Status.ToString(),
                    BranchName = j.Branch.NameAr,
                    TotalAmount = j.Lines.Sum(l => l.DebitAmount),
                    LinesCount = j.Lines.Count
                }).ToList()
            };

            return View(viewModel);
        }

        // GET: JournalEntries/Create
        [Authorize(Policy = "journal.create")]
        public async Task<IActionResult> Create()
        {
            var viewModel = new CreateJournalEntryViewModel
            {
                Date = DateTime.Now,
                Number = await GenerateJournalEntryNumber()
            };

            await PopulateDropdowns(viewModel);
            return View(viewModel);
        }

        private async Task<string> GenerateJournalEntryNumber()
        {
            var year = DateTime.Now.Year;
            var lastEntry = await _context.JournalEntries
                .Where(j => j.Date.Year == year)
                .OrderByDescending(j => j.Number)
                .FirstOrDefaultAsync();

            if (lastEntry == null)
                return $"JE{year}001";

            var lastNumber = lastEntry.Number.Substring(6);
            if (int.TryParse(lastNumber, out int number))
                return $"JE{year}{(number + 1):D3}";

            return $"JE{year}001";
        }

        private async Task PopulateDropdowns(CreateJournalEntryViewModel model)
        {
            model.Branches = await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                }).ToListAsync();

            model.CostCenters = await _context.CostCenters
                .Select(c => new SelectListItem
                {
                    Value = c.Id.ToString(),
                    Text = c.NameAr
                }).ToListAsync();

            model.Accounts = await _context.Accounts
                .Where(a => a.CanPostTransactions)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}"
                }).ToListAsync();
        }

        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> Draft()
        {
            return await GetEntriesByStatus(JournalEntryStatus.Draft, "Draft");
        }

        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> Posted()
        {
            return await GetEntriesByStatus(JournalEntryStatus.Posted, "Posted");
        }

        private async Task<IActionResult> GetEntriesByStatus(JournalEntryStatus status, string viewName)
        {
            var journalEntries = await _context.JournalEntries
                .Include(j => j.Branch)
                .Include(j => j.Lines)
                .Where(j => j.Status == status)
                .OrderByDescending(j => j.Date)
                .ToListAsync();

            var viewModel = new JournalEntriesIndexViewModel
            {
                JournalEntries = journalEntries.Select(j => new JournalEntryViewModel
                {
                    Id = j.Id,
                    Number = j.Number,
                    Date = j.Date,
                    Description = j.Description,
                    Reference = j.Reference,
                    Status = j.Status.ToString(),
                    BranchName = j.Branch.NameAr,
                    TotalAmount = j.Lines.Sum(l => l.DebitAmount),
                    LinesCount = j.Lines.Count
                }).ToList()
            };

            return View(viewName, viewModel);
        }
    }
}
