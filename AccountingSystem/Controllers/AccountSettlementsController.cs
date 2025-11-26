using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
    [Authorize]
    public class AccountSettlementsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public AccountSettlementsController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? accountId)
        {
            var accounts = await _context.Accounts
                .AsNoTracking()
                .Where(a => a.IsActive && a.CanPostTransactions)
                .OrderBy(a => a.Code)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {(string.IsNullOrWhiteSpace(a.NameAr) ? a.NameEn ?? string.Empty : a.NameAr)}"
                })
                .ToListAsync();

            var model = new AccountSettlementIndexViewModel
            {
                AccountId = accountId,
                Accounts = accounts,
                AccountName = accounts.FirstOrDefault(a => a.Value == accountId?.ToString())?.Text,
                SettlementDate = DateTime.Now
            };

            if (accountId.HasValue)
            {
                var unsettledLinesQuery = _context.JournalEntryLines
                    .AsNoTracking()
                    .Include(l => l.JournalEntry)
                    .Where(l => l.AccountId == accountId.Value)
                    .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted || l.JournalEntry.Status == JournalEntryStatus.Approved)
                    .Where(l => !_context.AccountSettlementPairs.Any(p => p.DebitLineId == l.Id || p.CreditLineId == l.Id));

                var unsettledLines = await unsettledLinesQuery
                    .OrderBy(l => l.JournalEntry.Date)
                    .ThenBy(l => l.Id)
                    .ToListAsync();

                model.DebitLines = unsettledLines
                    .Where(l => l.DebitAmount > 0)
                    .Select(l => new AccountSettlementLineViewModel
                    {
                        LineId = l.Id,
                        Date = l.JournalEntry.Date,
                        JournalNumber = l.JournalEntry.Number,
                        Description = string.IsNullOrWhiteSpace(l.Description) ? l.JournalEntry.Description : l.Description,
                        Debit = l.DebitAmount,
                        Credit = l.CreditAmount,
                        Reference = l.Reference
                    })
                    .ToList();

                model.CreditLines = unsettledLines
                    .Where(l => l.CreditAmount > 0)
                    .Select(l => new AccountSettlementLineViewModel
                    {
                        LineId = l.Id,
                        Date = l.JournalEntry.Date,
                        JournalNumber = l.JournalEntry.Number,
                        Description = string.IsNullOrWhiteSpace(l.Description) ? l.JournalEntry.Description : l.Description,
                        Debit = l.DebitAmount,
                        Credit = l.CreditAmount,
                        Reference = l.Reference
                    })
                    .ToList();

                var settledPairs = await _context.AccountSettlementPairs
                    .AsNoTracking()
                    .Include(p => p.Settlement)
                    .Include(p => p.DebitLine)!.ThenInclude(l => l.JournalEntry)
                    .Include(p => p.CreditLine)!.ThenInclude(l => l.JournalEntry)
                    .Where(p => p.Settlement.AccountId == accountId.Value)
                    .OrderByDescending(p => p.Settlement.CreatedAt)
                    .ThenByDescending(p => p.Id)
                    .ToListAsync();

                model.SettledPairs = settledPairs
                    .Select(p => new AccountSettlementPairViewModel
                    {
                        PairId = p.Id,
                        CreatedAt = p.Settlement.CreatedAt,
                        DebitLine = new AccountSettlementLineViewModel
                        {
                            LineId = p.DebitLineId,
                            Date = p.DebitLine.JournalEntry.Date,
                            JournalNumber = p.DebitLine.JournalEntry.Number,
                            Description = string.IsNullOrWhiteSpace(p.DebitLine.Description) ? p.DebitLine.JournalEntry.Description : p.DebitLine.Description,
                            Debit = p.DebitLine.DebitAmount,
                            Credit = p.DebitLine.CreditAmount,
                            Reference = p.DebitLine.Reference
                        },
                        CreditLine = new AccountSettlementLineViewModel
                        {
                            LineId = p.CreditLineId,
                            Date = p.CreditLine.JournalEntry.Date,
                            JournalNumber = p.CreditLine.JournalEntry.Number,
                            Description = string.IsNullOrWhiteSpace(p.CreditLine.Description) ? p.CreditLine.JournalEntry.Description : p.CreditLine.Description,
                            Debit = p.CreditLine.DebitAmount,
                            Credit = p.CreditLine.CreditAmount,
                            Reference = p.CreditLine.Reference
                        }
                    })
                    .ToList();
            }

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Settle(AccountSettlementRequest request)
        {
            if (request.AccountId <= 0)
            {
                TempData["Error"] = "يرجى اختيار حساب لإتمام التسوية.";
                return RedirectToAction(nameof(Index));
            }

            var account = await _context.Accounts.FindAsync(request.AccountId);
            if (account == null)
            {
                return NotFound();
            }

            var debitIds = request.SelectedDebitIds?.Distinct().ToList() ?? new List<int>();
            var creditIds = request.SelectedCreditIds?.Distinct().ToList() ?? new List<int>();

            if (!debitIds.Any() || !creditIds.Any())
            {
                TempData["Error"] = "يجب اختيار حركة مدينة وأخرى دائنة لإتمام التسوية.";
                return RedirectToAction(nameof(Index), new { accountId = request.AccountId });
            }

            var validDebitLines = await _context.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => debitIds.Contains(l.Id) && l.AccountId == request.AccountId)
                .Where(l => l.DebitAmount > 0)
                .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted || l.JournalEntry.Status == JournalEntryStatus.Approved)
                .Where(l => !_context.AccountSettlementPairs.Any(p => p.DebitLineId == l.Id || p.CreditLineId == l.Id))
                .OrderBy(l => debitIds.IndexOf(l.Id))
                .ToListAsync();

            var validCreditLines = await _context.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => creditIds.Contains(l.Id) && l.AccountId == request.AccountId)
                .Where(l => l.CreditAmount > 0)
                .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted || l.JournalEntry.Status == JournalEntryStatus.Approved)
                .Where(l => !_context.AccountSettlementPairs.Any(p => p.DebitLineId == l.Id || p.CreditLineId == l.Id))
                .OrderBy(l => creditIds.IndexOf(l.Id))
                .ToListAsync();

            var pairCount = Math.Min(validDebitLines.Count, validCreditLines.Count);

            if (pairCount == 0)
            {
                TempData["Error"] = "تعذر إيجاد حركات مطابقة للتسوية. تأكد من أن الحركات غير مسوّاة ومطابقة للحساب.";
                return RedirectToAction(nameof(Index), new { accountId = request.AccountId });
            }

            var user = await _userManager.GetUserAsync(User);

            var settlementDate = request.SettlementDate ?? DateTime.Now;

            var settlement = new AccountSettlement
            {
                AccountId = request.AccountId,
                CreatedAt = settlementDate,
                CreatedById = user?.Id
            };

            for (var i = 0; i < pairCount; i++)
            {
                settlement.Pairs.Add(new AccountSettlementPair
                {
                    DebitLineId = validDebitLines[i].Id,
                    CreditLineId = validCreditLines[i].Id
                });
            }

            _context.AccountSettlements.Add(settlement);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم نقل الحركات المحددة إلى كشف التسويات بنجاح.";
            return RedirectToAction(nameof(Index), new { accountId = request.AccountId });
        }
    }
}
