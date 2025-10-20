using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc.Rendering;
using ClosedXML.Excel;
using System.Collections.Generic;
using System.IO;
using System;
using System.Linq;
using System.Text.Json;
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

            var userAccounts = await _context.UserPaymentAccounts
                .Where(u => u.UserId == user.Id)
                .Include(u => u.Account).ThenInclude(a => a.Branch)
                .Include(u => u.Account).ThenInclude(a => a.Currency)
                .ToListAsync();

            var accounts = userAccounts
                .Select(u => u.Account)
                .Where(a => a != null)
                .GroupBy(a => a!.Id)
                .Select(g => g.First()!)
                .ToList();

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
                AccountName = selectedAccount.NameAr,
                BranchName = selectedAccount.Branch?.NameAr ?? string.Empty,
                OpeningBalance = openingBalance,
                TodayTransactions = todayTransactions,
                CumulativeBalance = selectedAccount.CurrentBalance
            };

            await PopulateAccountDataAsync(model, accounts, selectedAccount);

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

            var userAccounts = await _context.UserPaymentAccounts
                .Where(u => u.UserId == user.Id)
                .Include(u => u.Account).ThenInclude(a => a.Branch)
                .Include(u => u.Account).ThenInclude(a => a.Currency)
                .ToListAsync();

            var accounts = userAccounts
                .Select(u => u.Account)
                .Where(a => a != null)
                .GroupBy(a => a!.Id)
                .Select(g => g.First()!)
                .ToList();

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
                if (account != null)
                {
                    model.AccountName = account.NameAr;
                    model.BranchName = account.Branch?.NameAr ?? string.Empty;
                    model.OpeningBalance = openingBalance;
                    model.TodayTransactions = todayTransactions;
                    model.CumulativeBalance = account.CurrentBalance;
                    model.CurrencyId = account.CurrencyId;
                    model.CurrencyCode = account.Currency?.Code ?? string.Empty;
                }

                await PopulateAccountDataAsync(model, accounts, account);
                return View(model);
            }

            var breakdownMap = model.CurrencyUnitCounts?
                .Where(c => c.CurrencyUnitId > 0 && c.Count > 0)
                .GroupBy(c => c.CurrencyUnitId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Count))
                ?? new Dictionary<int, int>();

            decimal finalCountedAmount = model.CountedAmount;
            string? breakdownJson = null;

            if (breakdownMap.Any())
            {
                var units = await _context.CurrencyUnits
                    .Where(u => breakdownMap.Keys.Contains(u.Id))
                    .Select(u => new { u.Id, u.CurrencyId, u.ValueInBaseUnit })
                    .ToListAsync();

                if (units.Count != breakdownMap.Count || units.Any(u => account == null || u.CurrencyId != account.CurrencyId))
                {
                    ModelState.AddModelError(string.Empty, "بيانات الفئات غير صحيحة.");
                    if (account != null)
                    {
                        model.CurrencyId = account.CurrencyId;
                        model.CurrencyCode = account.Currency?.Code ?? string.Empty;
                    }
                    await PopulateAccountDataAsync(model, accounts, account);
                    return View(model);
                }

                decimal amountFromUnits = 0m;
                foreach (var unit in units)
                {
                    amountFromUnits += unit.ValueInBaseUnit * breakdownMap[unit.Id];
                }

                if (amountFromUnits <= 0)
                {
                    ModelState.AddModelError(string.Empty, "المبلغ الناتج عن الفئات يجب أن يكون أكبر من صفر.");
                    if (account != null)
                    {
                        model.CurrencyId = account.CurrencyId;
                        model.CurrencyCode = account.Currency?.Code ?? string.Empty;
                    }
                    await PopulateAccountDataAsync(model, accounts, account);
                    return View(model);
                }

                finalCountedAmount = Math.Round(amountFromUnits, 2, MidpointRounding.AwayFromZero);
                model.CountedAmount = finalCountedAmount;
                breakdownJson = JsonSerializer.Serialize(breakdownMap);
            }

            var closure = new CashBoxClosure
            {
                UserId = user.Id,
                AccountId = account!.Id,
                BranchId = user.PaymentBranchId.Value,
                CountedAmount = finalCountedAmount,
                OpeningBalance = openingBalance,
                ClosingBalance = account.CurrentBalance,
                Notes = model.Notes,
                Status = CashBoxClosureStatus.Pending,
                CreatedAt = DateTime.Now,
                CurrencyBreakdownJson = breakdownJson
            };

            _context.CashBoxClosures.Add(closure);
            await _context.SaveChangesAsync();

            return RedirectToAction(nameof(MyClosures));
        }

        private async Task PopulateAccountDataAsync(CashBoxClosureCreateViewModel model, List<Account> accounts, Account? selectedAccount)
        {
            if (accounts == null || accounts.Count == 0)
            {
                model.Accounts = new List<SelectListItem>();
                model.AccountOptions = new List<CashBoxClosureCreateViewModel.AccountOption>();
                model.CurrencyUnits = new Dictionary<int, List<CashBoxClosureCreateViewModel.CurrencyUnitOption>>();
                model.CurrencyId = 0;
                model.CurrencyCode = string.Empty;
                return;
            }

            var selected = selectedAccount ?? accounts.FirstOrDefault(a => a.Id == model.AccountId) ?? accounts.First();

            model.Accounts = accounts
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}",
                    Selected = a.Id == selected.Id
                })
                .ToList();

            model.AccountOptions = accounts
                .Select(a => new CashBoxClosureCreateViewModel.AccountOption
                {
                    AccountId = a.Id,
                    DisplayName = $"{a.Code} - {a.NameAr}",
                    CurrencyId = a.CurrencyId,
                    CurrencyCode = a.Currency?.Code ?? string.Empty,
                    Selected = a.Id == selected.Id
                })
                .ToList();

            model.CurrencyId = selected.CurrencyId;
            model.CurrencyCode = selected.Currency?.Code ?? string.Empty;

            var currencyIds = accounts
                .Select(a => a.CurrencyId)
                .Distinct()
                .ToList();

            if (currencyIds.Count == 0)
            {
                model.CurrencyUnits = new Dictionary<int, List<CashBoxClosureCreateViewModel.CurrencyUnitOption>>();
                return;
            }

            var currencyUnits = await _context.CurrencyUnits
                .Where(u => currencyIds.Contains(u.CurrencyId))
                .OrderBy(u => u.ValueInBaseUnit)
                .Select(u => new
                {
                    u.Id,
                    u.CurrencyId,
                    u.Name,
                    u.ValueInBaseUnit
                })
                .ToListAsync();

            model.CurrencyUnits = currencyUnits
                .GroupBy(u => u.CurrencyId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(u => new CashBoxClosureCreateViewModel.CurrencyUnitOption
                    {
                        CurrencyUnitId = u.Id,
                        Name = u.Name,
                        ValueInBaseUnit = u.ValueInBaseUnit
                    }).ToList()
                );
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
            var breakdowns = new Dictionary<int, Dictionary<int, int>>();
            var unitIds = new HashSet<int>();

            foreach (var closure in closures)
            {
                if (string.IsNullOrEmpty(closure.CurrencyBreakdownJson))
                    continue;

                var parsed = JsonSerializer.Deserialize<Dictionary<int, int>>(closure.CurrencyBreakdownJson);
                if (parsed == null || parsed.Count == 0)
                    continue;

                breakdowns[closure.Id] = parsed;
                foreach (var unitId in parsed.Keys)
                {
                    unitIds.Add(unitId);
                }
            }

            Dictionary<int, string> unitNames = new();
            if (unitIds.Count > 0)
            {
                unitNames = await _context.CurrencyUnits
                    .Where(u => unitIds.Contains(u.Id))
                    .OrderByDescending(u => u.ValueInBaseUnit)
                    .ToDictionaryAsync(u => u.Id, u => $"{u.Name} ({u.ValueInBaseUnit:N2})");
            }

            ViewBag.CashClosureBreakdowns = breakdowns;
            ViewBag.CashClosureUnitNames = unitNames;
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
            var breakdowns = new Dictionary<int, Dictionary<int, int>>();
            var unitIds = new HashSet<int>();

            foreach (var closure in closures)
            {
                if (string.IsNullOrEmpty(closure.CurrencyBreakdownJson))
                    continue;

                var parsed = JsonSerializer.Deserialize<Dictionary<int, int>>(closure.CurrencyBreakdownJson);
                if (parsed == null || parsed.Count == 0)
                    continue;

                breakdowns[closure.Id] = parsed;
                foreach (var unitId in parsed.Keys)
                {
                    unitIds.Add(unitId);
                }
            }

            Dictionary<int, string> unitNames = new();
            if (unitIds.Count > 0)
            {
                unitNames = await _context.CurrencyUnits
                    .Where(u => unitIds.Contains(u.Id))
                    .OrderByDescending(u => u.ValueInBaseUnit)
                    .ToDictionaryAsync(u => u.Id, u => $"{u.Name} ({u.ValueInBaseUnit:N2})");
            }

            ViewBag.CashClosureBreakdowns = breakdowns;
            ViewBag.CashClosureUnitNames = unitNames;
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
                .Include(c => c.User)
                .AsQueryable();

            if (accountId.HasValue)
                query = query.Where(c => c.AccountId == accountId.Value);
            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(c => c.CreatedAt >= from);
            }
            if (toDate.HasValue)
            {
                var to = toDate.Value.Date.AddDays(1);
                query = query.Where(c => c.CreatedAt < to);
            }

            var closures = await query
                .OrderByDescending(c => c.CreatedAt)
                .ToListAsync();

            model.Closures = closures.Select(c =>
            {
                var expectedBalance = c.ClosingBalance - c.OpeningBalance;
                var difference = c.CountedAmount - expectedBalance;
                var differenceType = difference switch
                {
                    > 0 => "زيادة",
                    < 0 => "نقص",
                    _ => "بدون فرق"
                };

                return new CashBoxClosureReportItemViewModel
                {
                    CreatedAt = c.CreatedAt,
                    ClosingDate = c.ClosingDate,
                    UserName = c.User?.FullName ?? c.User?.UserName ?? string.Empty,
                    AccountName = c.Account?.NameAr ?? string.Empty,
                    BranchName = c.Branch?.NameAr ?? string.Empty,
                    OpeningBalance = c.OpeningBalance,
                    CountedAmount = c.CountedAmount,
                    ClosingBalance = c.ClosingBalance,
                    Difference = difference,
                    DifferenceType = differenceType,
                    Status = c.Status switch
                    {
                        CashBoxClosureStatus.Pending => "قيد الانتظار",
                        CashBoxClosureStatus.ApprovedMatched => "مطابق",
                        CashBoxClosureStatus.ApprovedWithDifference => "مع فرق",
                        CashBoxClosureStatus.Rejected => "مرفوض",
                        _ => c.Status.ToString()
                    },
                    Notes = c.Notes,
                    ApprovalNotes = c.Reason
                };
            }).ToList();

            return View(model);
        }

        [Authorize(Policy = "cashclosures.report")]
        public async Task<IActionResult> Export(int? accountId, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.CashBoxClosures
                .Include(c => c.Account)
                .Include(c => c.Branch)
                .Include(c => c.User)
                .AsQueryable();

            if (accountId.HasValue)
                query = query.Where(c => c.AccountId == accountId.Value);
            if (fromDate.HasValue)
            {
                var from = fromDate.Value.Date;
                query = query.Where(c => c.CreatedAt >= from);
            }
            if (toDate.HasValue)
            {
                var to = toDate.Value.Date.AddDays(1);
                query = query.Where(c => c.CreatedAt < to);
            }

            var closures = await query.OrderBy(c => c.CreatedAt).ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Closures");

            var headers = new[]
            {
                "التاريخ",
                "تاريخ الإغلاق",
                "المستخدم",
                "الحساب",
                "الفرع",
                "الرصيد الافتتاحي",
                "المبلغ المعدود",
                "الرصيد المتوقع",
                "الرصيد الختامي",
                "قيمة الفرق",
                "نوع الفرق",
                "الحالة",
                "ملاحظات",
                "ملاحظات الاعتماد"
            };

            for (var i = 0; i < headers.Length; i++)
            {
                worksheet.Cell(1, i + 1).Value = headers[i];
                worksheet.Cell(1, i + 1).Style.Font.Bold = true;
            }

            var row = 2;
            foreach (var closure in closures)
            {
                var expectedBalance = closure.ClosingBalance - closure.OpeningBalance;
                var difference = closure.CountedAmount - expectedBalance;
                var differenceType = difference switch
                {
                    > 0 => "زيادة",
                    < 0 => "نقص",
                    _ => "بدون فرق"
                };

                worksheet.Cell(row, 1).Value = closure.CreatedAt;
                worksheet.Cell(row, 1).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                worksheet.Cell(row, 2).Value = closure.ClosingDate;
                worksheet.Cell(row, 2).Style.DateFormat.Format = "dd/MM/yyyy HH:mm";
                worksheet.Cell(row, 3).Value = closure.User?.FullName ?? closure.User?.UserName ?? string.Empty;
                worksheet.Cell(row, 4).Value = closure.Account?.NameAr ?? string.Empty;
                worksheet.Cell(row, 5).Value = closure.Branch?.NameAr ?? string.Empty;
                worksheet.Cell(row, 6).Value = closure.OpeningBalance;
                worksheet.Cell(row, 6).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(row, 7).Value = closure.CountedAmount;
                worksheet.Cell(row, 7).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(row, 8).Value = expectedBalance;
                worksheet.Cell(row, 8).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(row, 9).Value = closure.ClosingBalance;
                worksheet.Cell(row, 9).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(row, 10).Value = difference;
                worksheet.Cell(row, 10).Style.NumberFormat.Format = "#,##0.00";
                worksheet.Cell(row, 11).Value = differenceType;
                worksheet.Cell(row, 12).Value = closure.Status switch
                {
                    CashBoxClosureStatus.Pending => "قيد الانتظار",
                    CashBoxClosureStatus.ApprovedMatched => "مطابق",
                    CashBoxClosureStatus.ApprovedWithDifference => "مع فرق",
                    CashBoxClosureStatus.Rejected => "مرفوض",
                    _ => closure.Status.ToString()
                };
                worksheet.Cell(row, 13).Value = closure.Notes ?? string.Empty;
                worksheet.Cell(row, 14).Value = closure.Reason ?? string.Empty;

                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "cashbox_closures.xlsx");
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

            var currentBalance = account.CurrentBalance;
            var difference = currentBalance - closure.CountedAmount;
            if (difference != 0)
            {
                var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "CashBoxDifferenceAccountId");
                if (setting == null || string.IsNullOrWhiteSpace(setting.Value))
                    return BadRequest("لم يتم إعداد حساب الفروقات");

                var diffAccount = await _context.Accounts
                    .FirstOrDefaultAsync(t => t.Code == setting.Value || t.Id.ToString() == setting.Value);

                if (diffAccount == null)
                    return BadRequest("لم يتم العثور على حساب الفروقات المحدد");

                if (diffAccount.CurrencyId != account.CurrencyId)
                    return BadRequest("يجب أن تكون عملة حساب الفروقات مطابقة لعملة الحساب المغلق");

                var diffAccountId = diffAccount.Id;
                var lines = new List<JournalEntryLine>();

                if (difference > 0)
                {
                    lines.Add(new JournalEntryLine
                    {
                        AccountId = closure.AccountId,
                        CreditAmount = difference,
                        Description = "قيد تسوية فرق إغلاق الصندوق"
                    });
                    lines.Add(new JournalEntryLine
                    {
                        AccountId = diffAccountId,
                        DebitAmount = difference,
                        Description = "زيادة قيد تسوية فرق إغلاق الصندوق"
                    });
                }
                else
                {
                    var absDiff = Math.Abs(difference);
                    lines.Add(new JournalEntryLine
                    {
                        AccountId = diffAccountId,
                        CreditAmount = absDiff,
                        Description = "قيد تسوية فرق إغلاق الصندوق نقص"
                    });
                    lines.Add(new JournalEntryLine
                    {
                        AccountId = closure.AccountId,
                        DebitAmount = absDiff,
                        Description = "قيد تسوية فرق إغلاق الصندوق"
                    });
                }

                await _journalEntryService.CreateJournalEntryAsync(
                    DateTime.Now,
                    "فرق إغلاق صندوق",
                    closure.BranchId,
                    user.Id,
                    lines,
                    JournalEntryStatus.Posted,
                    reference: $"CashBoxClosure:{closure.Id}");

                account.CurrentBalance = closure.CountedAmount;
                account.UpdatedAt = DateTime.Now;
            }

            closure.ClosingBalance = closure.CountedAmount;

            var zeroLines = new List<JournalEntryLine>
            {
                new JournalEntryLine { AccountId = closure.AccountId, DebitAmount = closure.ClosingBalance,Description=  "إغلاق صندوق" },
                new JournalEntryLine { AccountId = closure.AccountId, CreditAmount = closure.ClosingBalance,Description=  "إغلاق صندوق" }
            };

            await _journalEntryService.CreateJournalEntryAsync(
                DateTime.Now,
                "إغلاق صندوق",
                closure.BranchId,
                user.Id,
                zeroLines,
                JournalEntryStatus.Posted,
                reference: $"CashBoxClosure:{closure.Id}");

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
