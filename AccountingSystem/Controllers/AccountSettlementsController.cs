using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using ClosedXML.Excel;
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

        private async Task<List<AccountSettlementPair>> GetSettledPairsAsync(int accountId, DateTime? fromDate, DateTime? toDate)
        {
            var settledPairsQuery = _context.AccountSettlementPairs
                .AsNoTracking()
                .Include(p => p.Settlement)
                .Include(p => p.DebitLine)!.ThenInclude(l => l.JournalEntry)
                .Include(p => p.CreditLine)!.ThenInclude(l => l.JournalEntry)
                .Where(p => p.Settlement.AccountId == accountId);

            if (fromDate.HasValue)
            {
                settledPairsQuery = settledPairsQuery
                    .Where(p => p.Settlement.CreatedAt.Date >= fromDate.Value.Date);
            }

            if (toDate.HasValue)
            {
                var toDateExclusive = toDate.Value.Date.AddDays(1);
                settledPairsQuery = settledPairsQuery
                    .Where(p => p.Settlement.CreatedAt < toDateExclusive);
            }

            return await settledPairsQuery
                .OrderByDescending(p => p.Settlement.CreatedAt)
                .ThenByDescending(p => p.Id)
                .ToListAsync();
        }

        [HttpGet]
        public async Task<IActionResult> Index(int? accountId, DateTime? fromDate, DateTime? toDate)
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
                SettlementDate = toDate ?? DateTime.Now,
                FromDate = fromDate,
                ToDate = toDate
            };

            if (accountId.HasValue)
            {
                var unsettledLinesQuery = _context.JournalEntryLines
                    .AsNoTracking()
                    .Include(l => l.JournalEntry)
                    .Where(l => l.AccountId == accountId.Value)
                    .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted || l.JournalEntry.Status == JournalEntryStatus.Approved)
                    .Where(l => !_context.AccountSettlementPairs.Any(p => p.DebitLineId == l.Id || p.CreditLineId == l.Id));

                if (fromDate.HasValue)
                {
                    unsettledLinesQuery = unsettledLinesQuery
                        .Where(l => l.JournalEntry.Date >= fromDate.Value.Date);
                }

                if (toDate.HasValue)
                {
                    var toDateExclusive = toDate.Value.Date.AddDays(1);
                    unsettledLinesQuery = unsettledLinesQuery
                        .Where(l => l.JournalEntry.Date < toDateExclusive);
                }

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

                var settledPairs = await GetSettledPairsAsync(accountId.Value, fromDate, toDate);

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

            var debitOrderLookup = debitIds
                .Select((id, index) => new { id, index })
                .ToDictionary(x => x.id, x => x.index);

            var creditOrderLookup = creditIds
                .Select((id, index) => new { id, index })
                .ToDictionary(x => x.id, x => x.index);

            var validDebitLines = await _context.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => debitIds.Contains(l.Id) && l.AccountId == request.AccountId)
                .Where(l => l.DebitAmount > 0)
                .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted || l.JournalEntry.Status == JournalEntryStatus.Approved)
                .Where(l => !_context.AccountSettlementPairs.Any(p => p.DebitLineId == l.Id || p.CreditLineId == l.Id))
                .ToListAsync();

            validDebitLines = validDebitLines
                .OrderBy(l => debitOrderLookup.TryGetValue(l.Id, out var index) ? index : int.MaxValue)
                .ToList();

            var validCreditLines = await _context.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Where(l => creditIds.Contains(l.Id) && l.AccountId == request.AccountId)
                .Where(l => l.CreditAmount > 0)
                .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted || l.JournalEntry.Status == JournalEntryStatus.Approved)
                .Where(l => !_context.AccountSettlementPairs.Any(p => p.DebitLineId == l.Id || p.CreditLineId == l.Id))
                .ToListAsync();

            validCreditLines = validCreditLines
                .OrderBy(l => creditOrderLookup.TryGetValue(l.Id, out var index) ? index : int.MaxValue)
                .ToList();

            if (!validDebitLines.Any() || !validCreditLines.Any())
            {
                TempData["Error"] = "تعذر إيجاد حركات مطابقة للتسوية. تأكد من أن الحركات غير مسوّاة ومطابقة للحساب.";
                return RedirectToAction(nameof(Index), new { accountId = request.AccountId });
            }

            var debitTotal = validDebitLines.Sum(l => l.DebitAmount);
            var creditTotal = validCreditLines.Sum(l => l.CreditAmount);

            if (debitTotal != creditTotal)
            {
                TempData["Error"] = $"يجب أن يتساوى إجمالي الحركات المدينة والدائنة قبل التسوية. إجمالي المدين: {debitTotal:N2}، إجمالي الدائن: {creditTotal:N2}.";
                return RedirectToAction(nameof(Index), new { accountId = request.AccountId });
            }

            var user = await _userManager.GetUserAsync(User);

            var settlementDate = request.SettlementDate ?? request.ToDate ?? DateTime.Now;

            var settlement = new AccountSettlement
            {
                AccountId = request.AccountId,
                CreatedAt = settlementDate,
                CreatedById = user?.Id
            };

            var pairCount = Math.Max(validDebitLines.Count, validCreditLines.Count);

            for (var i = 0; i < pairCount; i++)
            {
                var debitLine = i < validDebitLines.Count
                    ? validDebitLines[i]
                    : validDebitLines.Last();

                var creditLine = i < validCreditLines.Count
                    ? validCreditLines[i]
                    : validCreditLines.Last();

                settlement.Pairs.Add(new AccountSettlementPair
                {
                    DebitLineId = debitLine.Id,
                    CreditLineId = creditLine.Id
                });
            }

            _context.AccountSettlements.Add(settlement);
            await _context.SaveChangesAsync();

            TempData["Success"] = "تم نقل الحركات المحددة إلى كشف التسويات بنجاح.";
            return RedirectToAction(nameof(Index), new
            {
                accountId = request.AccountId,
                fromDate = request.FromDate?.ToString("yyyy-MM-dd"),
                toDate = request.ToDate?.ToString("yyyy-MM-dd")
            });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePair(int pairId, int accountId, DateTime? fromDate, DateTime? toDate)
        {
            var pair = await _context.AccountSettlementPairs
                .Include(p => p.Settlement)
                .FirstOrDefaultAsync(p => p.Id == pairId);

            if (pair == null)
            {
                return NotFound();
            }

            var settlementId = pair.AccountSettlementId;
            _context.AccountSettlementPairs.Remove(pair);
            await _context.SaveChangesAsync();

            var hasRemainingPairs = await _context.AccountSettlementPairs
                .AnyAsync(p => p.AccountSettlementId == settlementId);

            if (!hasRemainingPairs)
            {
                var settlement = await _context.AccountSettlements.FindAsync(settlementId);
                if (settlement != null)
                {
                    _context.AccountSettlements.Remove(settlement);
                    await _context.SaveChangesAsync();
                }
            }

            TempData["Success"] = "تم إلغاء التسوية المحددة وإرجاع الحركات لتصبح قابلة للتسوية مرة أخرى.";

            return RedirectToAction(nameof(Index), new
            {
                accountId,
                fromDate = fromDate?.ToString("yyyy-MM-dd"),
                toDate = toDate?.ToString("yyyy-MM-dd")
            });
        }

        [HttpGet]
        public async Task<IActionResult> ExportToExcel(int? accountId, DateTime? fromDate, DateTime? toDate)
        {
            if (accountId == null)
            {
                TempData["Error"] = "يرجى اختيار حساب قبل التصدير.";
                return RedirectToAction(nameof(Index));
            }

            var account = await _context.Accounts.FindAsync(accountId.Value);
            if (account == null)
            {
                return NotFound();
            }

            var settledPairs = await GetSettledPairsAsync(accountId.Value, fromDate, toDate);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Account Settlements");

            worksheet.Cell(1, 1).Value = "الحساب";
            worksheet.Cell(1, 2).Value = string.IsNullOrWhiteSpace(account.NameAr) ? account.NameEn : account.NameAr;
            worksheet.Cell(2, 1).Value = "الفترة";
            worksheet.Cell(2, 2).Value = fromDate.HasValue || toDate.HasValue
                ? $"{fromDate?.ToString("yyyy-MM-dd") ?? "---"} إلى {toDate?.ToString("yyyy-MM-dd") ?? "---"}"
                : "كل الفترات";

            worksheet.Cell(4, 1).Value = "تاريخ التسوية";
            worksheet.Cell(4, 2).Value = "بيان الحركة المدينة";
            worksheet.Cell(4, 3).Value = "رقم قيد المدين";
            worksheet.Cell(4, 4).Value = "المبلغ المدين";
            worksheet.Cell(4, 5).Value = "بيان الحركة الدائنة";
            worksheet.Cell(4, 6).Value = "رقم قيد الدائن";
            worksheet.Cell(4, 7).Value = "المبلغ الدائن";

            var currentRow = 5;

            foreach (var pair in settledPairs)
            {
                worksheet.Cell(currentRow, 1).Value = pair.Settlement.CreatedAt.ToString("yyyy-MM-dd HH:mm");
                worksheet.Cell(currentRow, 2).Value = pair.DebitLine.Description;
                worksheet.Cell(currentRow, 3).Value = pair.DebitLine.JournalEntry.Number;
                worksheet.Cell(currentRow, 4).Value = pair.DebitLine.DebitAmount;
                worksheet.Cell(currentRow, 5).Value = pair.CreditLine.Description;
                worksheet.Cell(currentRow, 6).Value = pair.CreditLine.JournalEntry.Number;
                worksheet.Cell(currentRow, 7).Value = pair.CreditLine.CreditAmount;
                currentRow++;
            }

            worksheet.Range(4, 1, 4, 7).Style.Font.SetBold();
            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"تسويات_الحساب_{account.Code}_{DateTime.Now:yyyyMMddHHmm}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }
    }
}
