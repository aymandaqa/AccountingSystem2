using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using System.Text;
using System.Collections.Generic;
using System;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using AccountingSystem.Services;

namespace AccountingSystem.Controllers
{
    [Authorize]
    public class CashBoxClosuresController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IJournalEntryService _journalEntryService;

        public CashBoxClosuresController(ApplicationDbContext context, UserManager<User> userManager, IJournalEntryService journalEntryService)
        {
            _context = context;
            _userManager = userManager;
            _journalEntryService = journalEntryService;
        }

        [Authorize(Policy = "cashclosures.create")]
        public async Task<IActionResult> Create(int? accountId)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound();

            var accounts = await _context.UserPaymentAccounts
                .Where(u => u.UserId == user.Id)
                .Include(u => u.Account).ThenInclude(a => a.Branch)
                .Select(u => u.Account)
                .ToListAsync();

            if (accounts.Count == 0)
                return NotFound();

            var selectedAccount = accounts.FirstOrDefault(a => a.Id == (accountId ?? accounts.First().Id));
            if (selectedAccount == null)
                return NotFound();

            var today = DateTime.Today;
            var todayTransactions = await _context.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => l.AccountId == selectedAccount.Id)
                .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted)
                .Where(l => l.JournalEntry.Date >= today && l.JournalEntry.Date < today.AddDays(1))
                .SumAsync(l => l.DebitAmount - l.CreditAmount);

            var openingBalance = selectedAccount.CurrentBalance - todayTransactions;

            var model = new CashBoxClosureCreateViewModel
            {
                AccountId = selectedAccount.Id,
                Accounts = accounts.Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}"
                }).ToList(),
                AccountName = selectedAccount.NameAr,
                BranchName = selectedAccount.Branch?.NameAr ?? string.Empty,
                OpeningBalance = openingBalance,
                TodayTransactions = todayTransactions,
                CumulativeBalance = selectedAccount.CurrentBalance
            };

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "cashclosures.create")]
        public async Task<IActionResult> Create(CashBoxClosureCreateViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentBranchId == null)
                return NotFound();

            var accounts = await _context.UserPaymentAccounts
                .Where(u => u.UserId == user.Id)
                .Include(u => u.Account).ThenInclude(a => a.Branch)
                .Select(u => u.Account)
                .ToListAsync();

            var account = accounts.FirstOrDefault(a => a.Id == model.AccountId);
            if (account == null)
                ModelState.AddModelError("AccountId", "الحساب غير موجود");

            decimal todayTransactions = 0m;
            decimal openingBalance = 0m;

            if (account != null)
            {
                var today = DateTime.Today;
                todayTransactions = await _context.JournalEntryLines
                    .Include(l => l.JournalEntry)
                    .Where(l => l.AccountId == account.Id)
                    .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted)
                    .Where(l => l.JournalEntry.Date >= today && l.JournalEntry.Date < today.AddDays(1))
                    .SumAsync(l => l.DebitAmount - l.CreditAmount);

                openingBalance = account.CurrentBalance - todayTransactions;
            }

            if (!ModelState.IsValid)
            {
                model.Accounts = accounts.Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}"
                }).ToList();

                if (account != null)
                {
                    model.AccountName = account.NameAr;
                    model.BranchName = account.Branch?.NameAr ?? string.Empty;
                    model.OpeningBalance = openingBalance;
                    model.TodayTransactions = todayTransactions;
                    model.CumulativeBalance = account.CurrentBalance;
                }

                return View(model);
            }

            var closure = new CashBoxClosure
            {
                UserId = user.Id,
                AccountId = account!.Id,
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

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return NotFound();

            var account = await _context.Accounts.FindAsync(closure.AccountId);
            if (account == null)
                return NotFound();

            var difference = closure.CountedAmount - (account.CurrentBalance - closure.OpeningBalance);
            if (!matched && difference != 0)
            {
                var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "CashBoxDifferenceAccountId");
                if (setting == null || !int.TryParse(setting.Value, out var diffAccountId))
                    return BadRequest("لم يتم إعداد حساب الفروقات");

                var lines = new List<JournalEntryLine>();
                if (difference > 0)
                {
                    lines.Add(new JournalEntryLine { AccountId = closure.AccountId, DebitAmount = difference });
                    lines.Add(new JournalEntryLine { AccountId = diffAccountId, CreditAmount = difference });
                }
                else
                {
                    var absDiff = Math.Abs(difference);
                    lines.Add(new JournalEntryLine { AccountId = diffAccountId, DebitAmount = absDiff });
                    lines.Add(new JournalEntryLine { AccountId = closure.AccountId, CreditAmount = absDiff });
                }

                await _journalEntryService.CreateJournalEntryAsync(
                    DateTime.Now,
                    "فرق إغلاق صندوق",
                    closure.BranchId,
                    user.Id,
                    lines,
                    JournalEntryStatus.Posted);
            }

            closure.ClosingBalance = account.CurrentBalance;

            var zeroLines = new List<JournalEntryLine>
            {
                new JournalEntryLine { AccountId = closure.AccountId, DebitAmount = closure.ClosingBalance },
                new JournalEntryLine { AccountId = closure.AccountId, CreditAmount = closure.ClosingBalance }
            };

            await _journalEntryService.CreateJournalEntryAsync(
                DateTime.Now,
                "إغلاق صندوق",
                closure.BranchId,
                user.Id,
                zeroLines,
                JournalEntryStatus.Posted);

            closure.Status = matched ? CashBoxClosureStatus.ApprovedMatched : CashBoxClosureStatus.ApprovedWithDifference;
            closure.Reason = reason;
            closure.ApprovedAt = DateTime.Now;
            closure.ClosingDate = DateTime.Now;

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
