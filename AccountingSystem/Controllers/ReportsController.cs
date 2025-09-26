using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;
using ClosedXML.Excel;
using QuestPDF.Fluent;
using QuestPDF.Infrastructure;
using QuestPDF.Helpers;
using AccountingSystem.Services;
using System;
using System.Security.Claims;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "reports.view")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly ICurrencyService _currencyService;

        public ReportsController(ApplicationDbContext context, ICurrencyService currencyService)
        {
            _context = context;
            _currencyService = currencyService;
        }

        // GET: Reports
        public IActionResult Index()
        {
            return View();
        }

        [Authorize(Policy = "reports.dynamic")]
        public IActionResult DynamicPivot()
        {
            var viewModel = new DynamicPivotReportViewModel
            {
                ReportTypes = Enum.GetValues<DynamicReportType>()
                    .Select(t => new SelectListItem
                    {
                        Value = t.ToString(),
                        Text = GetReportTypeDisplayName(t)
                    })
                    .ToList()
            };

            return View(viewModel);
        }

        [Authorize(Policy = "reports.dynamic")]
        [HttpGet]
        public async Task<IActionResult> GetPivotReports(DynamicReportType reportType)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var reports = await _context.PivotReports
                .AsNoTracking()
                .Where(r => r.ReportType == reportType && r.CreatedById == userId)
                .OrderBy(r => r.Name)
                .Select(r => new PivotReportListItemViewModel
                {
                    Id = r.Id,
                    Name = r.Name,
                    ReportType = r.ReportType,
                    UpdatedAt = r.UpdatedAt ?? r.CreatedAt
                })
                .ToListAsync();

            return Json(reports);
        }

        [Authorize(Policy = "reports.dynamic")]
        [HttpGet]
        public async Task<IActionResult> GetPivotReport(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var report = await _context.PivotReports
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && r.CreatedById == userId);

            if (report == null)
            {
                return NotFound();
            }

            return Json(new
            {
                report.Id,
                report.Name,
                ReportType = report.ReportType.ToString(),
                report.Layout
            });
        }

        [Authorize(Policy = "reports.dynamic")]
        [HttpGet]
        public async Task<IActionResult> GetDynamicPivotData(DynamicReportType reportType, DateTime? fromDate, DateTime? toDate)
        {
            var from = fromDate ?? DateTime.Today.AddMonths(-1);
            var to = toDate ?? DateTime.Today;

            switch (reportType)
            {
                case DynamicReportType.JournalEntries:
                    var journalData = await _context.JournalEntryLines
                        .AsNoTracking()
                        .Include(l => l.JournalEntry).ThenInclude(e => e.Branch)
                        .Include(l => l.Account).ThenInclude(a => a.Branch)
                        .Include(l => l.CostCenter)
                        .Where(l => l.JournalEntry.Date >= from && l.JournalEntry.Date <= to)
                        .Select(l => new
                        {
                            l.JournalEntryId,
                            EntryNumber = l.JournalEntry.Number,
                            EntryDate = l.JournalEntry.Date,
                            EntryYear = l.JournalEntry.Date.Year,
                            EntryMonth = l.JournalEntry.Date.Month,
                            EntryStatus = l.JournalEntry.Status.ToString(),
                            BranchCode = l.JournalEntry.Branch.Code,
                            BranchName = l.JournalEntry.Branch.NameAr,
                            AccountCode = l.Account.Code,
                            AccountName = l.Account.NameAr,
                            AccountBranch = l.Account.Branch != null ? l.Account.Branch.NameAr : null,
                            CostCenter = l.CostCenter != null ? l.CostCenter.NameAr : null,
                            LineDescription = l.Description,
                            Reference = l.Reference,
                            Debit = l.DebitAmount,
                            Credit = l.CreditAmount
                        })
                        .ToListAsync();
                    return Json(journalData);

                case DynamicReportType.ReceiptVouchers:
                    var receiptData = await _context.ReceiptVouchers
                        .AsNoTracking()
                        .Include(r => r.Account).ThenInclude(a => a.Branch)
                        .Include(r => r.Currency)
                        .Include(r => r.CreatedBy)
                        .Where(r => r.Date >= from && r.Date <= to)
                        .Select(r => new
                        {
                            r.Id,
                            r.Date,
                            Year = r.Date.Year,
                            Month = r.Date.Month,
                            AccountCode = r.Account.Code,
                            AccountName = r.Account.NameAr,
                            BranchCode = r.Account.Branch != null ? r.Account.Branch.Code : null,
                            BranchName = r.Account.Branch != null ? r.Account.Branch.NameAr : null,
                            Currency = r.Currency.Code,
                            r.Amount,
                            r.ExchangeRate,
                            AmountBase = r.Amount * r.ExchangeRate,
                            CreatedBy = r.CreatedBy.UserName,
                            r.Notes
                        })
                        .ToListAsync();
                    return Json(receiptData);

                case DynamicReportType.PaymentVouchers:
                    var paymentData = await _context.PaymentVouchers
                        .AsNoTracking()
                        .Include(v => v.Supplier)
                        .Include(v => v.Account).ThenInclude(a => a!.Branch)
                        .Include(v => v.Currency)
                        .Include(v => v.CreatedBy)
                        .Where(v => v.Date >= from && v.Date <= to)
                        .Select(v => new
                        {
                            v.Id,
                            v.Date,
                            Year = v.Date.Year,
                            Month = v.Date.Month,
                            Supplier = v.Supplier.NameAr,
                            AccountCode = v.Account != null ? v.Account.Code : null,
                            AccountName = v.Account != null ? v.Account.NameAr : null,
                            BranchCode = v.Account != null && v.Account.Branch != null ? v.Account.Branch.Code : null,
                            BranchName = v.Account != null && v.Account.Branch != null ? v.Account.Branch.NameAr : null,
                            Currency = v.Currency.Code,
                            v.Amount,
                            v.ExchangeRate,
                            AmountBase = v.Amount * v.ExchangeRate,
                            CreatedBy = v.CreatedBy.UserName,
                            v.IsCash,
                            v.Notes
                        })
                        .ToListAsync();
                    return Json(paymentData);

                case DynamicReportType.DisbursementVouchers:
                    var disbursementData = await _context.DisbursementVouchers
                        .AsNoTracking()
                        .Include(v => v.Supplier)
                        .Include(v => v.Account).ThenInclude(a => a.Branch)
                        .Include(v => v.Currency)
                        .Include(v => v.CreatedBy)
                        .Where(v => v.Date >= from && v.Date <= to)
                        .Select(v => new
                        {
                            v.Id,
                            v.Date,
                            Year = v.Date.Year,
                            Month = v.Date.Month,
                            Supplier = v.Supplier.NameAr,
                            AccountCode = v.Account.Code,
                            AccountName = v.Account.NameAr,
                            BranchCode = v.Account.Branch != null ? v.Account.Branch.Code : null,
                            BranchName = v.Account.Branch != null ? v.Account.Branch.NameAr : null,
                            Currency = v.Currency.Code,
                            v.Amount,
                            v.ExchangeRate,
                            AmountBase = v.Amount * v.ExchangeRate,
                            CreatedBy = v.CreatedBy.UserName,
                            v.Notes
                        })
                        .ToListAsync();
                    return Json(disbursementData);

                default:
                    return Json(Array.Empty<object>());
            }
        }

        [Authorize(Policy = "reports.dynamic")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SavePivotReport([FromBody] SavePivotReportRequest request)
        {
            if (request == null)
            {
                return BadRequest(new { message = "بيانات غير صالحة" });
            }

            if (string.IsNullOrWhiteSpace(request.Name))
            {
                return BadRequest(new { message = "اسم التقرير مطلوب" });
            }

            if (string.IsNullOrWhiteSpace(request.Layout))
            {
                return BadRequest(new { message = "لا توجد إعدادات للحفظ" });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            PivotReport? report;
            if (request.Id.HasValue)
            {
                report = await _context.PivotReports
                    .FirstOrDefaultAsync(r => r.Id == request.Id.Value && r.CreatedById == userId);

                if (report == null)
                {
                    return NotFound();
                }

                report.Name = request.Name.Trim();
                report.Layout = request.Layout;
                report.ReportType = request.ReportType;
                report.UpdatedAt = DateTime.UtcNow;
            }
            else
            {
                report = new PivotReport
                {
                    Name = request.Name.Trim(),
                    Layout = request.Layout,
                    ReportType = request.ReportType,
                    CreatedById = userId,
                    CreatedAt = DateTime.UtcNow
                };

                _context.PivotReports.Add(report);
            }

            await _context.SaveChangesAsync();

            return Ok(new { message = "تم حفظ التقرير بنجاح", report.Id, report.Name });
        }

        [Authorize(Policy = "reports.dynamic")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeletePivotReport([FromBody] DeletePivotReportRequest request)
        {
            if (request == null || request.Id <= 0)
            {
                return BadRequest(new { message = "بيانات غير صالحة" });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var report = await _context.PivotReports
                .FirstOrDefaultAsync(r => r.Id == request.Id && r.CreatedById == userId);

            if (report == null)
            {
                return NotFound();
            }

            _context.PivotReports.Remove(report);
            await _context.SaveChangesAsync();

            return Ok(new { message = "تم حذف التقرير" });
        }

        private static string GetReportTypeDisplayName(DynamicReportType type)
        {
            return type switch
            {
                DynamicReportType.JournalEntries => "قيود اليومية",
                DynamicReportType.ReceiptVouchers => "سندات القبض",
                DynamicReportType.PaymentVouchers => "سندات الدفع",
                DynamicReportType.DisbursementVouchers => "سندات الصرف",
                _ => type.ToString()
            };
        }

        // GET: Reports/TrialBalance
        public async Task<IActionResult> TrialBalance(int? branchId, DateTime? fromDate, DateTime? toDate, bool includePending = false, int? currencyId = null)
        {
            var accounts = await _context.Accounts
                .Include(a => a.Branch)
                .Include(a => a.Currency)
                .Where(a => a.CanPostTransactions)
                .Where(a => !branchId.HasValue || a.BranchId == branchId || a.BranchId == null)
                .OrderBy(a => a.Code)
                .ToListAsync();

            var from = fromDate ?? DateTime.Now.AddMonths(-1);
            var to = toDate ?? DateTime.Now;

            var pending = includePending
                ? await _context.JournalEntryLines
                    .Include(l => l.JournalEntry)
                    .Where(l => l.JournalEntry.Status != JournalEntryStatus.Posted)
                    .Where(l => l.JournalEntry.Date >= from && l.JournalEntry.Date <= to)
                    .Where(l => !branchId.HasValue || l.JournalEntry.BranchId == branchId)
                    .GroupBy(l => l.AccountId)
                    .Select(g => new { g.Key, Debit = g.Sum(x => x.DebitAmount), Credit = g.Sum(x => x.CreditAmount) })
                    .ToDictionaryAsync(x => x.Key, x => (x.Debit, x.Credit))
                : new Dictionary<int, (decimal Debit, decimal Credit)>();

            var baseCurrency = await _context.Currencies.FirstAsync(c => c.IsBase);
            var selectedCurrency = currencyId.HasValue ? await _context.Currencies.FirstOrDefaultAsync(c => c.Id == currencyId.Value) : baseCurrency;
            selectedCurrency ??= baseCurrency;

            var viewModel = new TrialBalanceViewModel
            {
                FromDate = from,
                ToDate = to,
                BranchId = branchId,
                IncludePending = includePending,
                Accounts = accounts.Select(a =>
                {
                    pending.TryGetValue(a.Id, out var p);
                    var pendingBalance = a.Nature == AccountNature.Debit ? p.Debit - p.Credit : p.Credit - p.Debit;
                    var balance = a.CurrentBalance + pendingBalance;
                    var balanceSelected = _currencyService.Convert(balance, a.Currency, selectedCurrency);
                    var balanceBase = _currencyService.Convert(balance, a.Currency, baseCurrency);
                    return new TrialBalanceAccountViewModel
                    {
                        AccountCode = a.Code,
                        AccountName = a.NameAr,
                        DebitBalance = a.Nature == AccountNature.Debit ? balanceSelected : 0,
                        CreditBalance = a.Nature == AccountNature.Credit ? balanceSelected : 0,
                        DebitBalanceBase = a.Nature == AccountNature.Debit ? balanceBase : 0,
                        CreditBalanceBase = a.Nature == AccountNature.Credit ? balanceBase : 0
                    };
                }).ToList(),
                Branches = await GetBranchesSelectList(),
                Currencies = await _context.Currencies
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Code })
                    .ToListAsync(),
                SelectedCurrencyId = selectedCurrency.Id,
                SelectedCurrencyCode = selectedCurrency.Code,
                BaseCurrencyCode = baseCurrency.Code
            };

            viewModel.TotalDebits = viewModel.Accounts.Sum(a => a.DebitBalance);
            viewModel.TotalCredits = viewModel.Accounts.Sum(a => a.CreditBalance);
            viewModel.TotalDebitsBase = viewModel.Accounts.Sum(a => a.DebitBalanceBase);
            viewModel.TotalCreditsBase = viewModel.Accounts.Sum(a => a.CreditBalanceBase);

            return View(viewModel);
        }

        // GET: Reports/PendingTransactions
        [Authorize(Policy = "reports.pending")]
        public async Task<IActionResult> PendingTransactions(int? branchId, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Include(l => l.Account)
                .Where(l => l.JournalEntry.Status != JournalEntryStatus.Posted)
                .Where(l => !branchId.HasValue || l.JournalEntry.BranchId == branchId)
                .Where(l => !fromDate.HasValue || l.JournalEntry.Date >= fromDate)
                .Where(l => !toDate.HasValue || l.JournalEntry.Date <= toDate);

            var accounts = await query
                .GroupBy(l => new { l.Account.Code, l.Account.NameAr })
                .Select(g => new TrialBalanceAccountViewModel
                {
                    AccountCode = g.Key.Code,
                    AccountName = g.Key.NameAr,
                    DebitBalance = g.Sum(x => x.DebitAmount),
                    CreditBalance = g.Sum(x => x.CreditAmount)
                })
                .OrderBy(a => a.AccountCode)
                .ToListAsync();

            var viewModel = new PendingTransactionsViewModel
            {
                FromDate = fromDate ?? DateTime.Now.AddMonths(-1),
                ToDate = toDate ?? DateTime.Now,
                BranchId = branchId,
                Accounts = accounts,
                Branches = await GetBranchesSelectList()
            };

            viewModel.TotalDebits = viewModel.Accounts.Sum(a => a.DebitBalance);
            viewModel.TotalCredits = viewModel.Accounts.Sum(a => a.CreditBalance);

            return View(viewModel);
        }

        // GET: Reports/BalanceSheet
        public async Task<IActionResult> BalanceSheet(int? branchId, DateTime? asOfDate, bool includePending = false, int? currencyId = null)
        {
            var viewModel = await BuildBalanceSheetViewModel(branchId, asOfDate ?? DateTime.Now, includePending, currencyId);
            return View(viewModel);
        }

        // GET: Reports/BalanceSheetPdf
        public async Task<IActionResult> BalanceSheetPdf(int? branchId, DateTime? asOfDate, bool includePending = false, int? currencyId = null)
        {
            var model = await BuildBalanceSheetViewModel(branchId, asOfDate ?? DateTime.Now, includePending, currencyId);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Size(PageSizes.A4);
                    page.Header().Text($"الميزانية العمومية - {model.AsOfDate:yyyy-MM-dd}").FontSize(16).Bold();
                    page.Content().Column(col =>
                    {
                        col.Item().Text("الأصول").FontSize(14).Bold();
                        ComposePdfTree(col, model.Assets, 0, model.SelectedCurrencyCode, model.BaseCurrencyCode);
                        col.Item().Text($"إجمالي الأصول: {model.TotalAssets:N2} {model.SelectedCurrencyCode} ({model.TotalAssetsBase:N2} {model.BaseCurrencyCode})");

                        col.Item().PaddingTop(10).Text("الخصوم").FontSize(14).Bold();
                        ComposePdfTree(col, model.Liabilities, 0, model.SelectedCurrencyCode, model.BaseCurrencyCode);
                        col.Item().Text($"إجمالي الخصوم: {model.TotalLiabilities:N2} {model.SelectedCurrencyCode} ({model.TotalLiabilitiesBase:N2} {model.BaseCurrencyCode})");

                        col.Item().PaddingTop(10).Text("حقوق الملكية").FontSize(14).Bold();
                        ComposePdfTree(col, model.Equity, 0, model.SelectedCurrencyCode, model.BaseCurrencyCode);
                        col.Item().Text($"إجمالي حقوق الملكية: {model.TotalEquity:N2} {model.SelectedCurrencyCode} ({model.TotalEquityBase:N2} {model.BaseCurrencyCode})");
                    });
                });
            });

            static void ComposePdfTree(ColumnDescriptor col, List<AccountTreeNodeViewModel> nodes, int level, string selectedCurrencyCode, string baseCurrencyCode)
            {
                foreach (var node in nodes)
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(level * 15);
                        row.RelativeItem().Text(node.Id == 0 ? node.NameAr : $"{node.Code} - {node.NameAr}");
                        row.ConstantItem(150).AlignRight().Text($"{node.BalanceSelected:N2} {selectedCurrencyCode} ({node.BalanceBase:N2} {baseCurrencyCode})");
                    });
                    if (node.Children.Any())
                        ComposePdfTree(col, node.Children, level + 1, selectedCurrencyCode, baseCurrencyCode);
                }
            }

            var pdf = document.GeneratePdf();
            return File(pdf, "application/pdf", "BalanceSheet.pdf");
        }

        // GET: Reports/BalanceSheetExcel
        public async Task<IActionResult> BalanceSheetExcel(int? branchId, DateTime? asOfDate, bool includePending = false, int? currencyId = null)
        {
            var model = await BuildBalanceSheetViewModel(branchId, asOfDate ?? DateTime.Now, includePending, currencyId);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet("BalanceSheet");
            var row = 1;
            worksheet.Cell(row, 1).Value = "الحساب";
            worksheet.Cell(row, 2).Value = $"الرصيد ({model.SelectedCurrencyCode})";
            worksheet.Cell(row, 3).Value = $"الرصيد ({model.BaseCurrencyCode})";
            row++;

            void WriteNodes(List<AccountTreeNodeViewModel> nodes, int level)
            {
                foreach (var node in nodes)
                {
                    worksheet.Cell(row, 1).Value = new string(' ', level * 2) + (node.Id == 0 ? node.NameAr : $"{node.Code} - {node.NameAr}");
                    worksheet.Cell(row, 2).Value = node.BalanceSelected;
                    worksheet.Cell(row, 3).Value = node.BalanceBase;
                    row++;
                    if (node.Children.Any())
                        WriteNodes(node.Children, level + 1);
                }
            }

            WriteNodes(model.Assets, 0);
            worksheet.Cell(row, 1).Value = "إجمالي الأصول";
            worksheet.Cell(row, 2).Value = model.TotalAssets;
            worksheet.Cell(row, 3).Value = model.TotalAssetsBase;
            row++;
            WriteNodes(model.Liabilities, 0);
            worksheet.Cell(row, 1).Value = "إجمالي الخصوم";
            worksheet.Cell(row, 2).Value = model.TotalLiabilities;
            worksheet.Cell(row, 3).Value = model.TotalLiabilitiesBase;
            row++;
            WriteNodes(model.Equity, 0);
            worksheet.Cell(row, 1).Value = "إجمالي حقوق الملكية";
            worksheet.Cell(row, 2).Value = model.TotalEquity;
            worksheet.Cell(row, 3).Value = model.TotalEquityBase;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BalanceSheet.xlsx");
        }

        private async Task<BalanceSheetViewModel> BuildBalanceSheetViewModel(int? branchId, DateTime asOfDate, bool includePending, int? currencyId)
        {
            var accounts = await _context.Accounts
                .Include(a => a.JournalEntryLines)
                    .ThenInclude(l => l.JournalEntry)
                .Include(a => a.Currency)
                .Where(a => a.Classification == AccountClassification.BalanceSheet)
                .Where(a => !branchId.HasValue || a.BranchId == branchId || a.BranchId == null)
                .AsNoTracking()
                .ToListAsync();

            var baseCurrency = await _context.Currencies.FirstAsync(c => c.IsBase);
            var selectedCurrency = currencyId.HasValue ? await _context.Currencies.FirstOrDefaultAsync(c => c.Id == currencyId.Value) : baseCurrency;
            selectedCurrency ??= baseCurrency;

            var balances = accounts.ToDictionary(a => a.Id, a =>
                a.OpeningBalance + a.JournalEntryLines
                    .Where(l => includePending || l.JournalEntry.Status == JournalEntryStatus.Posted)
                    .Where(l => l.JournalEntry.Date <= asOfDate)
                    .Where(l => !branchId.HasValue || l.JournalEntry.BranchId == branchId)
                    .Sum(l => l.DebitAmount - l.CreditAmount));

            var nodes = accounts.Select(a =>
            {
                var balance = balances[a.Id];
                return new AccountTreeNodeViewModel
                {
                    Id = a.Id,
                    Code = a.Code,
                    NameAr = a.NameAr,
                    AccountType = a.AccountType,
                    Nature = a.Nature,
                    CurrencyCode = a.Currency.Code,
                    OpeningBalance = a.OpeningBalance,
                    Balance = balance,
                    BalanceSelected = _currencyService.Convert(balance, a.Currency, selectedCurrency),
                    BalanceBase = _currencyService.Convert(balance, a.Currency, baseCurrency),
                    IsActive = a.IsActive,
                    CanPostTransactions = a.CanPostTransactions,
                    ParentId = a.ParentId,
                    Level = a.Level,
                    Children = new List<AccountTreeNodeViewModel>(),
                    HasChildren = false
                };
            }).ToDictionary(n => n.Id);

            foreach (var node in nodes.Values)
            {
                if (node.ParentId.HasValue && nodes.TryGetValue(node.ParentId.Value, out var parent))
                {
                    parent.Children.Add(node);
                    parent.HasChildren = true;
                }
            }

            void ComputeBalances(AccountTreeNodeViewModel node)
            {
                foreach (var child in node.Children)
                {
                    ComputeBalances(child);
                }
                if (node.Children.Any())
                {
                    node.Balance = node.Children.Sum(c => c.Balance);
                    node.BalanceSelected = node.Children.Sum(c => c.BalanceSelected);
                    node.BalanceBase = node.Children.Sum(c => c.BalanceBase);
                }
            }

            var rootNodes = nodes.Values.Where(n => n.ParentId == null).ToList();
            foreach (var root in rootNodes)
            {
                ComputeBalances(root);
            }

            var assets = rootNodes.Where(n => n.AccountType == AccountType.Assets).OrderBy(n => n.Code).ToList();
            var liabilities = rootNodes.Where(n => n.AccountType == AccountType.Liabilities).OrderBy(n => n.Code).ToList();
            var equity = rootNodes.Where(n => n.AccountType == AccountType.Equity).OrderBy(n => n.Code).ToList();

            var viewModel = new BalanceSheetViewModel
            {
                AsOfDate = asOfDate,
                BranchId = branchId,
                IncludePending = includePending,
                Assets = assets,
                Liabilities = liabilities,
                Equity = equity,
                Branches = await GetBranchesSelectList(),
                SelectedCurrencyId = selectedCurrency.Id,
                SelectedCurrencyCode = selectedCurrency.Code,
                BaseCurrencyCode = baseCurrency.Code,
                Currencies = await _context.Currencies.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Code }).ToListAsync()
            };

            viewModel.TotalAssets = assets.Sum(a => a.BalanceSelected);
            viewModel.TotalLiabilities = liabilities.Sum(l => l.BalanceSelected);
            viewModel.TotalEquity = equity.Sum(e => e.BalanceSelected);
            viewModel.TotalAssetsBase = assets.Sum(a => a.BalanceBase);
            viewModel.TotalLiabilitiesBase = liabilities.Sum(l => l.BalanceBase);
            viewModel.TotalEquityBase = equity.Sum(e => e.BalanceBase);
            viewModel.IsBalanced = viewModel.TotalAssetsBase == (viewModel.TotalLiabilitiesBase + viewModel.TotalEquityBase);

            return viewModel;
        }

        private async Task<IncomeStatementViewModel> BuildIncomeStatementViewModel(int? branchId, DateTime fromDate, DateTime toDate, bool includePending, int? currencyId)
        {
            var accounts = await _context.Accounts
                .Include(a => a.JournalEntryLines)
                    .ThenInclude(l => l.JournalEntry)
                .Include(a => a.Currency)
                .Where(a => a.Classification == AccountClassification.IncomeStatement)
                .Where(a => !branchId.HasValue || a.BranchId == branchId || a.BranchId == null)
                .AsNoTracking()
                .ToListAsync();

            var baseCurrency = await _context.Currencies.FirstAsync(c => c.IsBase);
            var selectedCurrency = currencyId.HasValue ? await _context.Currencies.FirstOrDefaultAsync(c => c.Id == currencyId.Value) : baseCurrency;
            selectedCurrency ??= baseCurrency;

            var balances = accounts.ToDictionary(a => a.Id, a =>
                a.JournalEntryLines
                    .Where(l => includePending || l.JournalEntry.Status == JournalEntryStatus.Posted)
                    .Where(l => l.JournalEntry.Date >= fromDate && l.JournalEntry.Date <= toDate)
                    .Where(l => !branchId.HasValue || l.JournalEntry.BranchId == branchId)
                    .Sum(l => a.Nature == AccountNature.Debit ? l.DebitAmount - l.CreditAmount : l.CreditAmount - l.DebitAmount));

            var nodes = accounts.Select(a =>
            {
                var balance = balances[a.Id];
                return new AccountTreeNodeViewModel
                {
                    Id = a.Id,
                    Code = a.Code,
                    NameAr = a.NameAr,
                    AccountType = a.AccountType,
                    Nature = a.Nature,
                    CurrencyCode = a.Currency.Code,
                    Balance = balance,
                    BalanceSelected = _currencyService.Convert(balance, a.Currency, selectedCurrency),
                    BalanceBase = _currencyService.Convert(balance, a.Currency, baseCurrency),
                    ParentId = a.ParentId,
                    Level = a.Level,
                    Children = new List<AccountTreeNodeViewModel>(),
                    HasChildren = false
                };
            }).ToDictionary(n => n.Id);

            foreach (var node in nodes.Values)
            {
                if (node.ParentId.HasValue && nodes.TryGetValue(node.ParentId.Value, out var parent))
                {
                    parent.Children.Add(node);
                    parent.HasChildren = true;
                }
            }

            void ComputeBalances(AccountTreeNodeViewModel node)
            {
                foreach (var child in node.Children)
                {
                    ComputeBalances(child);
                }
                if (node.Children.Any())
                {
                    node.Balance = node.Children.Sum(c => c.Balance);
                    node.BalanceSelected = node.Children.Sum(c => c.BalanceSelected);
                    node.BalanceBase = node.Children.Sum(c => c.BalanceBase);
                }
            }

            var rootNodes = nodes.Values.Where(n => n.ParentId == null).ToList();
            foreach (var root in rootNodes)
            {
                ComputeBalances(root);
            }

            var revenues = rootNodes.Where(n => n.AccountType == AccountType.Revenue).OrderBy(n => n.Code).ToList();
            var expenses = rootNodes.Where(n => n.AccountType == AccountType.Expenses).OrderBy(n => n.Code).ToList();

            var viewModel = new IncomeStatementViewModel
            {
                FromDate = fromDate,
                ToDate = toDate,
                BranchId = branchId,
                IncludePending = includePending,
                Revenues = revenues,
                Expenses = expenses,
                Branches = await GetBranchesSelectList(),
                SelectedCurrencyId = selectedCurrency.Id,
                SelectedCurrencyCode = selectedCurrency.Code,
                BaseCurrencyCode = baseCurrency.Code,
                Currencies = await _context.Currencies.Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Code }).ToListAsync()
            };

            viewModel.TotalRevenues = revenues.Sum(r => r.BalanceSelected);
            viewModel.TotalExpenses = expenses.Sum(e => e.BalanceSelected);
            viewModel.NetIncome = viewModel.TotalRevenues - viewModel.TotalExpenses;
            viewModel.TotalRevenuesBase = revenues.Sum(r => r.BalanceBase);
            viewModel.TotalExpensesBase = expenses.Sum(e => e.BalanceBase);
            viewModel.NetIncomeBase = viewModel.TotalRevenuesBase - viewModel.TotalExpensesBase;

            return viewModel;
        }

        // GET: Reports/IncomeStatement
        public async Task<IActionResult> IncomeStatement(int? branchId, DateTime? fromDate, DateTime? toDate, bool includePending = false, int? currencyId = null)
        {
            var model = await BuildIncomeStatementViewModel(
                branchId,
                fromDate ?? DateTime.Now.AddMonths(-1),
                toDate ?? DateTime.Now,
                includePending,
                currencyId);
            return View(model);
        }

        // GET: Reports/IncomeStatementPdf
        public async Task<IActionResult> IncomeStatementPdf(int? branchId, DateTime? fromDate, DateTime? toDate, bool includePending = false, int? currencyId = null)
        {
            var model = await BuildIncomeStatementViewModel(
                branchId,
                fromDate ?? DateTime.Now.AddMonths(-1),
                toDate ?? DateTime.Now,
                includePending,
                currencyId);

            var document = Document.Create(container =>
            {
                container.Page(page =>
                {
                    page.Margin(20);
                    page.Size(PageSizes.A4);
                    page.Header().Text($"قائمة الدخل - {model.FromDate:yyyy-MM-dd} إلى {model.ToDate:yyyy-MM-dd}").FontSize(16).Bold();
                    page.Content().Column(col =>
                    {
                        col.Item().Text("الإيرادات").FontSize(14).Bold();
                        ComposePdfTree(col, model.Revenues, 0, model.SelectedCurrencyCode, model.BaseCurrencyCode);
                        col.Item().Text($"إجمالي الإيرادات: {model.TotalRevenues:N2} {model.SelectedCurrencyCode} ({model.TotalRevenuesBase:N2} {model.BaseCurrencyCode})");

                        col.Item().PaddingTop(10).Text("المصروفات").FontSize(14).Bold();
                        ComposePdfTree(col, model.Expenses, 0, model.SelectedCurrencyCode, model.BaseCurrencyCode);
                        col.Item().Text($"إجمالي المصروفات: {model.TotalExpenses:N2} {model.SelectedCurrencyCode} ({model.TotalExpensesBase:N2} {model.BaseCurrencyCode})");

                        col.Item().PaddingTop(10).Text($"صافي الدخل: {model.NetIncome:N2} {model.SelectedCurrencyCode} ({model.NetIncomeBase:N2} {model.BaseCurrencyCode})").FontSize(14).Bold();
                    });
                });
            });

            static void ComposePdfTree(ColumnDescriptor col, List<AccountTreeNodeViewModel> nodes, int level, string selectedCurrencyCode, string baseCurrencyCode)
            {
                foreach (var node in nodes)
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(level * 15);
                        row.RelativeItem().Text(node.Id == 0 ? node.NameAr : $"{node.Code} - {node.NameAr}");
                        row.ConstantItem(150).AlignRight().Text($"{node.BalanceSelected:N2} {selectedCurrencyCode} ({node.BalanceBase:N2} {baseCurrencyCode})");
                    });
                    if (node.Children.Any())
                        ComposePdfTree(col, node.Children, level + 1, selectedCurrencyCode, baseCurrencyCode);
                }
            }

            var pdf = document.GeneratePdf();
            return File(pdf, "application/pdf", "IncomeStatement.pdf");
        }

        // GET: Reports/IncomeStatementExcel
        public async Task<IActionResult> IncomeStatementExcel(int? branchId, DateTime? fromDate, DateTime? toDate, bool includePending = false, int? currencyId = null)
        {
            var model = await BuildIncomeStatementViewModel(
                branchId,
                fromDate ?? DateTime.Now.AddMonths(-1),
                toDate ?? DateTime.Now,
                includePending,
                currencyId);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet("IncomeStatement");
            var row = 1;
            worksheet.Cell(row, 1).Value = "الحساب";
            worksheet.Cell(row, 2).Value = $"المبلغ ({model.SelectedCurrencyCode})";
            worksheet.Cell(row, 3).Value = $"المبلغ ({model.BaseCurrencyCode})";
            row++;

            void WriteNodes(List<AccountTreeNodeViewModel> nodes, int level)
            {
                foreach (var node in nodes)
                {
                    worksheet.Cell(row, 1).Value = new string(' ', level * 2) + (node.Id == 0 ? node.NameAr : $"{node.Code} - {node.NameAr}");
                    worksheet.Cell(row, 2).Value = node.BalanceSelected;
                    worksheet.Cell(row, 3).Value = node.BalanceBase;
                    row++;
                    if (node.Children.Any())
                        WriteNodes(node.Children, level + 1);
                }
            }

            WriteNodes(model.Revenues, 0);
            worksheet.Cell(row, 1).Value = "إجمالي الإيرادات";
            worksheet.Cell(row, 2).Value = model.TotalRevenues;
            worksheet.Cell(row, 3).Value = model.TotalRevenuesBase;
            row++;
            WriteNodes(model.Expenses, 0);
            worksheet.Cell(row, 1).Value = "إجمالي المصروفات";
            worksheet.Cell(row, 2).Value = model.TotalExpenses;
            worksheet.Cell(row, 3).Value = model.TotalExpensesBase;
            row++;
            worksheet.Cell(row, 1).Value = "صافي الدخل";
            worksheet.Cell(row, 2).Value = model.NetIncome;
            worksheet.Cell(row, 3).Value = model.NetIncomeBase;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "IncomeStatement.xlsx");
        }

        // GET: Reports/AccountStatement
        public async Task<IActionResult> AccountStatement(int? accountId, int? branchId, DateTime? fromDate, DateTime? toDate)
        {
            var baseCurrency = await _context.Currencies.FirstAsync(c => c.IsBase);
            var viewModel = new AccountStatementViewModel
            {
                FromDate = fromDate ?? DateTime.Now.AddMonths(-1),
                ToDate = toDate ?? DateTime.Now,
                BranchId = branchId,
                Accounts = await _context.Accounts
                    .Where(a => a.CanPostTransactions)
                    .Select(a => new SelectListItem
                    {
                        Value = a.Id.ToString(),
                        Text = $"{a.Code} - {a.NameAr}"
                    }).ToListAsync(),
                Branches = await GetBranchesSelectList(),
                BaseCurrencyCode = baseCurrency.Code
            };

            if (accountId.HasValue)
            {
                var account = await _context.Accounts
                    .Include(a => a.Currency)
                    .FirstOrDefaultAsync(a => a.Id == accountId.Value);
                if (account != null)
                {
                    viewModel.AccountId = accountId;
                    viewModel.AccountCode = account.Code;
                    viewModel.AccountName = account.NameAr;
                    viewModel.CurrencyCode = account.Currency.Code;

                    var lines = await _context.JournalEntryLines
                        .Include(l => l.JournalEntry)
                        .Where(l => l.AccountId == accountId.Value)
                        .Where(l => !branchId.HasValue || l.JournalEntry.BranchId == branchId)
                        .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted)
                        .Where(l => l.JournalEntry.Date >= viewModel.FromDate && l.JournalEntry.Date <= viewModel.ToDate)
                        .OrderBy(l => l.JournalEntry.Date)
                        .ThenBy(l => l.JournalEntry.Number)
                        .ToListAsync();

                    decimal running = account.OpeningBalance;
                    decimal runningBase = _currencyService.Convert(running, account.Currency, baseCurrency);
                    foreach (var line in lines)
                    {
                        var debitBase = _currencyService.Convert(line.DebitAmount, account.Currency, baseCurrency);
                        var creditBase = _currencyService.Convert(line.CreditAmount, account.Currency, baseCurrency);
                        running += account.Nature == AccountNature.Debit
                            ? line.DebitAmount - line.CreditAmount
                            : line.CreditAmount - line.DebitAmount;
                        runningBase += account.Nature == AccountNature.Debit
                            ? debitBase - creditBase
                            : creditBase - debitBase;
                        viewModel.Transactions.Add(new AccountTransactionViewModel
                        {
                            Date = line.JournalEntry.Date,
                            JournalEntryNumber = line.JournalEntry.Number,
                            Description = line.Description ?? string.Empty,
                            DebitAmount = line.DebitAmount,
                            CreditAmount = line.CreditAmount,
                            RunningBalance = running,
                            DebitAmountBase = debitBase,
                            CreditAmountBase = creditBase,
                            RunningBalanceBase = runningBase
                        });
                    }

                    viewModel.OpeningBalance = account.OpeningBalance;
                    viewModel.OpeningBalanceBase = _currencyService.Convert(account.OpeningBalance, account.Currency, baseCurrency);
                    viewModel.ClosingBalance = running;
                    viewModel.ClosingBalanceBase = runningBase;
                    viewModel.TotalDebit = viewModel.Transactions.Sum(t => t.DebitAmount);
                    viewModel.TotalCredit = viewModel.Transactions.Sum(t => t.CreditAmount);
                    viewModel.TotalDebitBase = viewModel.Transactions.Sum(t => t.DebitAmountBase);
                    viewModel.TotalCreditBase = viewModel.Transactions.Sum(t => t.CreditAmountBase);
                }
            }

            return View(viewModel);
        }

        // GET: Reports/GeneralLedger
        public async Task<IActionResult> GeneralLedger(int? accountId, int? branchId, DateTime? fromDate, DateTime? toDate, bool includePending = false)
        {
            var from = fromDate ?? DateTime.Now.AddMonths(-1);
            var to = toDate ?? DateTime.Now;

            var lines = await _context.JournalEntryLines
                .Include(l => l.JournalEntry)
                .Include(l => l.Account)
                .Where(l => includePending || l.JournalEntry.Status == JournalEntryStatus.Posted)
                .Where(l => l.JournalEntry.Date >= from && l.JournalEntry.Date <= to)
                .Where(l => !branchId.HasValue || l.JournalEntry.BranchId == branchId)
                .Where(l => !accountId.HasValue || l.AccountId == accountId)
                .OrderBy(l => l.Account.Code)
                .ThenBy(l => l.JournalEntry.Date)
                .ToListAsync();

            var accounts = lines
                .GroupBy(l => l.Account)
                .Select(g => new GeneralLedgerAccountViewModel
                {
                    AccountCode = g.Key.Code,
                    AccountName = g.Key.NameAr,
                    Transactions = g.Select(l => new GeneralLedgerTransactionViewModel
                    {
                        Date = l.JournalEntry.Date,
                        JournalEntryNumber = l.JournalEntry.Number,
                        Description = l.Description ?? string.Empty,
                        DebitAmount = l.DebitAmount,
                        CreditAmount = l.CreditAmount
                    }).ToList()
                }).ToList();

            var viewModel = new GeneralLedgerViewModel
            {
                FromDate = from,
                ToDate = to,
                BranchId = branchId,
                AccountId = accountId,
                IncludePending = includePending,
                Accounts = accounts,
                Branches = await GetBranchesSelectList(),
                AccountOptions = await _context.Accounts
                    .Where(a => a.CanPostTransactions)
                    .Select(a => new SelectListItem
                    {
                        Value = a.Id.ToString(),
                        Text = $"{a.Code} - {a.NameAr}"
                    }).ToListAsync()
            };

            return View(viewModel);
        }

        private async Task<List<SelectListItem>> GetBranchesSelectList()
        {
            return await _context.Branches
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                }).ToListAsync();
        }
    }
}
