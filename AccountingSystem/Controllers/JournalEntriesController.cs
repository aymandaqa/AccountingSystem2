using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Security.Claims;

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
        public async Task<IActionResult> Index(string? searchTerm, int page = 1, int pageSize = 10)
        {
            var query = _context.JournalEntries
                .Include(j => j.Branch)
                .Include(j => j.Lines)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(j =>
                    j.Number.Contains(searchTerm) ||
                    j.Description.Contains(searchTerm) ||
                    (j.Reference != null && j.Reference.Contains(searchTerm)) ||
                    j.Branch.NameAr.Contains(searchTerm));
            }

            var totalItems = await query.CountAsync();

            var entries = await query
                .OrderByDescending(j => j.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = entries.Select(j => new JournalEntryViewModel
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
            }).ToList();

            var result = new PagedResult<JournalEntryViewModel>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                SearchTerm = searchTerm
            };

            return View(result);
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

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "journal.create")]
        public async Task<IActionResult> Create(CreateJournalEntryViewModel model)
        {
            if (!ModelState.IsValid)
            {
                await PopulateDropdowns(model);
                return View(model);
            }

            if (model.Lines == null || model.Lines.Count == 0 ||
                Math.Round(model.Lines.Sum(l => l.DebitAmount), 2) != Math.Round(model.Lines.Sum(l => l.CreditAmount), 2))
            {
                ModelState.AddModelError(string.Empty, "القيد غير متوازن");
                await PopulateDropdowns(model);
                return View(model);
            }

            var accountIds = model.Lines.Select(l => l.AccountId).Distinct().ToList();
            var currencies = await _context.Accounts
                .Where(a => accountIds.Contains(a.Id))
                .Select(a => a.CurrencyId)
                .Distinct()
                .ToListAsync();
            if (currencies.Count > 1)
            {
                ModelState.AddModelError(string.Empty, "يجب أن تكون جميع الحسابات بنفس العملة");
                await PopulateDropdowns(model);
                return View(model);
            }

            var entry = new JournalEntry
            {
                Number = model.Number,
                Date = model.Date,
                Description = model.Description,
                Reference = model.Reference,
                BranchId = model.BranchId,
                CreatedById = User.FindFirstValue(ClaimTypes.NameIdentifier) ?? string.Empty,
                TotalDebit = model.Lines.Sum(l => l.DebitAmount),
                TotalCredit = model.Lines.Sum(l => l.CreditAmount),

            };

            foreach (var line in model.Lines)
            {
                entry.Lines.Add(new JournalEntryLine
                {
                    AccountId = line.AccountId,
                    Description = line.Description,
                    DebitAmount = line.DebitAmount,
                    CreditAmount = line.CreditAmount,

                });
            }

            _context.JournalEntries.Add(entry);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
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
                    Text = $"{a.Code} - {a.NameAr} ({a.Currency.Code})"
                }).ToListAsync();
        }

        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> Details(int id)
        {
            var entry = await _context.JournalEntries
                .Include(j => j.Branch)
                .Include(j => j.Lines)
                    .ThenInclude(l => l.Account)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (entry == null)
                return NotFound();

            var model = new JournalEntryDetailsViewModel
            {
                Id = entry.Id,
                Number = entry.Number,
                Date = entry.Date,
                Description = entry.Description,
                Reference = entry.Reference,
                Status = entry.Status.ToString(),
                BranchName = entry.Branch.NameAr,
                Lines = entry.Lines.Select(l => new JournalEntryLineViewModel
                {
                    AccountId = l.AccountId,
                    AccountCode = l.Account.Code,
                    AccountName = $"{l.Account.NameAr} ({l.Account.Currency.Code})",
                    Description = l.Description ?? string.Empty,
                    DebitAmount = l.DebitAmount,
                    CreditAmount = l.CreditAmount
                }).ToList(),
                TotalDebit = entry.Lines.Sum(l => l.DebitAmount),
                TotalCredit = entry.Lines.Sum(l => l.CreditAmount)
            };

            return View(model);
        }

        // GET: JournalEntries/Edit/5
        [Authorize(Policy = "journal.edit")]
        public async Task<IActionResult> Edit(int id)
        {
            var entry = await _context.JournalEntries
                .Include(j => j.Lines)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (entry == null)
                return NotFound();

            var model = new CreateJournalEntryViewModel
            {
                Id = entry.Id,
                Number = entry.Number,
                Date = entry.Date,
                Description = entry.Description,
                Reference = entry.Reference,
                BranchId = entry.BranchId,
                Lines = entry.Lines.Select(l => new JournalEntryLineViewModel
                {
                    AccountId = l.AccountId,
                    Description = l.Description ?? string.Empty,
                    DebitAmount = l.DebitAmount,
                    CreditAmount = l.CreditAmount
                }).ToList()
            };

            await PopulateDropdowns(model);
            return View(model);
        }

        // POST: JournalEntries/Edit/5
        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "journal.edit")]
        public async Task<IActionResult> Edit(int id, CreateJournalEntryViewModel model)
        {
            if (id != model.Id)
                return NotFound();

            if (!ModelState.IsValid || model.Lines == null || model.Lines.Count == 0 ||
                Math.Round(model.Lines.Sum(l => l.DebitAmount), 2) != Math.Round(model.Lines.Sum(l => l.CreditAmount), 2))
            {
                if (Math.Round(model.Lines?.Sum(l => l.DebitAmount) ?? 0, 2) != Math.Round(model.Lines?.Sum(l => l.CreditAmount) ?? 0, 2))
                    ModelState.AddModelError(string.Empty, "القيد غير متوازن");

                await PopulateDropdowns(model);
                return View(model);
            }

            var accountIds = model.Lines.Select(l => l.AccountId).Distinct().ToList();
            var currencies = await _context.Accounts
                .Where(a => accountIds.Contains(a.Id))
                .Select(a => a.CurrencyId)
                .Distinct()
                .ToListAsync();
            if (currencies.Count > 1)
            {
                ModelState.AddModelError(string.Empty, "يجب أن تكون جميع الحسابات بنفس العملة");
                await PopulateDropdowns(model);
                return View(model);
            }

            var entry = await _context.JournalEntries
                .Include(j => j.Lines)
                .FirstOrDefaultAsync(j => j.Id == id);
            if (entry == null)
                return NotFound();

            entry.Date = model.Date;
            entry.Description = model.Description;
            entry.Reference = model.Reference;
            entry.BranchId = model.BranchId;
            entry.TotalDebit = model.Lines.Sum(l => l.DebitAmount);
            entry.TotalCredit = model.Lines.Sum(l => l.CreditAmount);
            entry.UpdatedAt = DateTime.Now;

            _context.JournalEntryLines.RemoveRange(entry.Lines);
            entry.Lines = model.Lines.Select(l => new JournalEntryLine
            {
                AccountId = l.AccountId,
                Description = l.Description,
                DebitAmount = l.DebitAmount,
                CreditAmount = l.CreditAmount
            }).ToList();

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "journal.edit")]
        public async Task<IActionResult> Post(int id)
        {
            var entry = await _context.JournalEntries
                .Include(j => j.Lines)
                    .ThenInclude(l => l.Account)
                .FirstOrDefaultAsync(j => j.Id == id);

            if (entry == null)
                return NotFound();

            if (entry.Status != JournalEntryStatus.Draft)
                return BadRequest();

            if (!entry.IsBalanced)
                return BadRequest("القيد غير متوازن");

            foreach (var line in entry.Lines)
            {
                var account = line.Account;
                var netAmount = account.Nature == AccountNature.Debit
                    ? line.DebitAmount - line.CreditAmount
                    : line.CreditAmount - line.DebitAmount;

                account.CurrentBalance += netAmount;
                account.UpdatedAt = DateTime.Now;
            }

            entry.Status = JournalEntryStatus.Posted;
            entry.UpdatedAt = DateTime.Now;

            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> Draft(string? searchTerm, int page = 1, int pageSize = 10)
        {
            return await GetEntriesByStatus(JournalEntryStatus.Draft, "Draft", searchTerm, page, pageSize);
        }

        [Authorize(Policy = "journal.view")]
        public async Task<IActionResult> Posted(string? searchTerm, int page = 1, int pageSize = 10)
        {
            return await GetEntriesByStatus(JournalEntryStatus.Posted, "Posted", searchTerm, page, pageSize);
        }

        private async Task<IActionResult> GetEntriesByStatus(JournalEntryStatus status, string viewName, string? searchTerm, int page, int pageSize)
        {
            var query = _context.JournalEntries
                .Include(j => j.Branch)
                .Include(j => j.Lines)
                .Where(j => j.Status == status)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                query = query.Where(j =>
                    j.Number.Contains(searchTerm) ||
                    j.Description.Contains(searchTerm) ||
                    (j.Reference != null && j.Reference.Contains(searchTerm)) ||
                    j.Branch.NameAr.Contains(searchTerm));
            }

            var totalItems = await query.CountAsync();

            var entries = await query
                .OrderByDescending(j => j.Date)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var items = entries.Select(j => new JournalEntryViewModel
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
            }).ToList();

            var result = new PagedResult<JournalEntryViewModel>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                SearchTerm = searchTerm
            };

            return View(viewName, result);
        }
    }
}
