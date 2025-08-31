using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;

namespace AccountingSystem.Controllers
{
    [Authorize]
    public class CashBoxClosuresController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public CashBoxClosuresController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [Authorize(Policy = "cashclosures.create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentAccountId == null || user.PaymentBranchId == null)
                return NotFound();

            var account = await _context.Accounts.FindAsync(user.PaymentAccountId);
            var branch = await _context.Branches.FindAsync(user.PaymentBranchId);
            if (account == null)
                return NotFound();

            var today = DateTime.Today;
            var todayTransactions = await _context.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => l.AccountId == account.Id)
                .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted)
                .Where(l => l.JournalEntry.Date >= today && l.JournalEntry.Date < today.AddDays(1))
                .SumAsync(l => l.DebitAmount - l.CreditAmount);

            var openingBalance = account.CurrentBalance - todayTransactions;

            var model = new CashBoxClosureCreateViewModel
            {
                AccountName = account?.NameAr ?? string.Empty,
                BranchName = branch?.NameAr ?? string.Empty,
                OpeningBalance = openingBalance,
                TodayTransactions = todayTransactions,
                CumulativeBalance = account.CurrentBalance
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "cashclosures.create")]
        public async Task<IActionResult> Create(CashBoxClosureCreateViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentAccountId == null || user.PaymentBranchId == null)
                return NotFound();

            var account = await _context.Accounts.FindAsync(user.PaymentAccountId);
            var branch = await _context.Branches.FindAsync(user.PaymentBranchId);
            if (account == null)
                return NotFound();

            var today = DateTime.Today;
            var todayTransactions = await _context.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => l.AccountId == account.Id)
                .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted)
                .Where(l => l.JournalEntry.Date >= today && l.JournalEntry.Date < today.AddDays(1))
                .SumAsync(l => l.DebitAmount - l.CreditAmount);

            var openingBalance = account.CurrentBalance - todayTransactions;

            if (!ModelState.IsValid)
            {
                model.AccountName = account.NameAr;
                model.BranchName = branch?.NameAr ?? string.Empty;
                model.OpeningBalance = openingBalance;
                model.TodayTransactions = todayTransactions;
                model.CumulativeBalance = account.CurrentBalance;
                return View(model);
            }

            var closure = new CashBoxClosure
            {
                UserId = user.Id,
                AccountId = account.Id,
                BranchId = user.PaymentBranchId.Value,
                CountedAmount = model.CountedAmount,
                OpeningBalance = openingBalance,
                ClosingBalance = account.CurrentBalance,
                Notes = model.Notes,
                Status = CashBoxClosureStatus.Pending,
                CreatedAt = DateTime.Now
            };

            _context.CashBoxClosures.Add(closure);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyClosures));
        }

        [Authorize(Policy = "cashclosures.view")]
        public async Task<IActionResult> MyClosures()
        {
            var userId = _userManager.GetUserId(User);
            var closures = await _context.CashBoxClosures
                .Include(c => c.Account)
                .Include(c => c.Branch)
                .Where(c => c.UserId == userId)
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();
            return View(closures);
        }

        [Authorize(Policy = "cashclosures.approve")]
        public async Task<IActionResult> Pending()
        {
            var closures = await _context.CashBoxClosures
                .Include(c => c.User)
                .Include(c => c.Account)
                .Include(c => c.Branch)
                .Where(c => c.Status == CashBoxClosureStatus.Pending)
                .OrderBy(c => c.CreatedAt)
                .ToListAsync();
            return View(closures);
        }

        [Authorize(Policy = "cashclosures.report")]
        public async Task<IActionResult> Report(int? accountId, DateTime? fromDate, DateTime? toDate)
        {
            var model = new CashBoxClosureReportViewModel
            {
                AccountId = accountId,
                FromDate = fromDate,
                ToDate = toDate,
                Accounts = await _context.Accounts
                    .Where(a => a.CanPostTransactions)
                    .Where(a => _context.Users.Any(u => u.PaymentAccountId == a.Id))
                    .OrderBy(a => a.Code)
                    .Select(a => new SelectListItem
                    {
                        Value = a.Id.ToString(),
                        Text = $"{a.Code} - {a.NameAr}"
                    }).ToListAsync()
            };

            var query = _context.CashBoxClosures
                .Include(c => c.Account)
                .Include(c => c.Branch)
                .AsQueryable();

            if (accountId.HasValue)
                query = query.Where(c => c.AccountId == accountId.Value);
            if (fromDate.HasValue)
                query = query.Where(c => c.CreatedAt >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(c => c.CreatedAt <= toDate.Value);

            var closures = await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            model.Closures = closures.Select(c => new CashBoxClosureReportItemViewModel
            {
                CreatedAt = c.CreatedAt,
                AccountName = c.Account?.NameAr ?? string.Empty,
                BranchName = c.Branch?.NameAr ?? string.Empty,
                OpeningBalance = c.OpeningBalance,
                CountedAmount = c.CountedAmount,
                ClosingBalance = c.ClosingBalance,
                Difference = c.CountedAmount - (c.ClosingBalance - c.OpeningBalance),
                Status = c.Status switch
                {
                    CashBoxClosureStatus.Pending => "قيد الانتظار",
                    CashBoxClosureStatus.ApprovedMatched => "مطابق",
                    CashBoxClosureStatus.ApprovedWithDifference => "مع فرق",
                    CashBoxClosureStatus.Rejected => "مرفوض",
                    _ => c.Status.ToString()
                }
            }).ToList();

            return View(model);
        }

        [Authorize(Policy = "cashclosures.report")]
        public async Task<IActionResult> Export(int? accountId, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.CashBoxClosures
                .Include(c => c.Account)
                .Include(c => c.Branch)
                .AsQueryable();

            if (accountId.HasValue)
                query = query.Where(c => c.AccountId == accountId.Value);
            if (fromDate.HasValue)
                query = query.Where(c => c.CreatedAt >= fromDate.Value);
            if (toDate.HasValue)
                query = query.Where(c => c.CreatedAt <= toDate.Value);

            var closures = await query.OrderBy(c => c.CreatedAt).ToListAsync();

            var sb = new StringBuilder();
            sb.AppendLine("Date,Account,Branch,OpeningBalance,CountedAmount,ClosingBalance,Difference,Status");
            foreach (var c in closures)
            {
                var diff = c.CountedAmount - (c.ClosingBalance - c.OpeningBalance);
                sb.AppendLine($"{c.CreatedAt:yyyy-MM-dd},{c.Account?.NameAr},{c.Branch?.NameAr},{c.OpeningBalance},{c.CountedAmount},{c.ClosingBalance},{diff},{c.Status}");
            }

            return File(Encoding.UTF8.GetBytes(sb.ToString()), "text/csv", "cashbox_closures.csv");
        }

        [HttpPost]
        [Authorize(Policy = "cashclosures.approve")]
        public async Task<IActionResult> Approve(int id, bool matched, string? reason)
        {
            var closure = await _context.CashBoxClosures.FindAsync(id);
            if (closure == null)
                return NotFound();

            var account = await _context.Accounts.FindAsync(closure.AccountId);
            closure.Status = matched ? CashBoxClosureStatus.ApprovedMatched : CashBoxClosureStatus.ApprovedWithDifference;
            closure.Reason = reason;
            closure.ApprovedAt = DateTime.Now;
            closure.ClosingDate = DateTime.Now;
            closure.ClosingBalance = account?.CurrentBalance ?? closure.ClosingBalance;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Pending));
        }

        [HttpPost]
        [Authorize(Policy = "cashclosures.approve")]
        public async Task<IActionResult> Reject(int id, string reason)
        {
            var closure = await _context.CashBoxClosures.FindAsync(id);
            if (closure == null)
                return NotFound();

            closure.Status = CashBoxClosureStatus.Rejected;
            closure.Reason = reason;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Pending));
        }
    }
}
