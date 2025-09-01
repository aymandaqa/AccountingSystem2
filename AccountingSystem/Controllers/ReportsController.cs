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

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "reports.view")]
    public class ReportsController : Controller
    {
        private readonly ApplicationDbContext _context;

        public ReportsController(ApplicationDbContext context)
        {
            _context = context;
        }

        // GET: Reports
        public IActionResult Index()
        {
            return View();
        }

        // GET: Reports/TrialBalance
        public async Task<IActionResult> TrialBalance(int? branchId, DateTime? fromDate, DateTime? toDate, bool includePending = false)
        {
            var accounts = await _context.Accounts
                .Include(a => a.Branch)
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
                    return new TrialBalanceAccountViewModel
                    {
                        AccountCode = a.Code,
                        AccountName = a.NameAr,
                        DebitBalance = a.Nature == AccountNature.Debit ? balance : 0,
                        CreditBalance = a.Nature == AccountNature.Credit ? balance : 0
                    };
                }).ToList(),
                Branches = await GetBranchesSelectList()
            };

            viewModel.TotalDebits = viewModel.Accounts.Sum(a => a.DebitBalance);
            viewModel.TotalCredits = viewModel.Accounts.Sum(a => a.CreditBalance);

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
        public async Task<IActionResult> BalanceSheet(int? branchId, DateTime? asOfDate, bool includePending = false)
        {
            var viewModel = await BuildBalanceSheetViewModel(branchId, asOfDate ?? DateTime.Now, includePending);
            return View(viewModel);
        }

        // GET: Reports/BalanceSheetPdf
        public async Task<IActionResult> BalanceSheetPdf(int? branchId, DateTime? asOfDate, bool includePending = false)
        {
            var model = await BuildBalanceSheetViewModel(branchId, asOfDate ?? DateTime.Now, includePending);

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
                        ComposePdfTree(col, model.Assets, 0);
                        col.Item().Text($"إجمالي الأصول: {model.TotalAssets:N2}");

                        col.Item().PaddingTop(10).Text("الخصوم").FontSize(14).Bold();
                        ComposePdfTree(col, model.Liabilities, 0);
                        col.Item().Text($"إجمالي الخصوم: {model.TotalLiabilities:N2}");

                        col.Item().PaddingTop(10).Text("حقوق الملكية").FontSize(14).Bold();
                        ComposePdfTree(col, model.Equity, 0);
                        col.Item().Text($"إجمالي حقوق الملكية: {model.TotalEquity:N2}");
                    });
                });
            });

            static void ComposePdfTree(ColumnDescriptor col, List<AccountTreeNodeViewModel> nodes, int level)
            {
                foreach (var node in nodes)
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(level * 15);
                        row.RelativeItem().Text(node.Id == 0 ? node.NameAr : $"{node.Code} - {node.NameAr}");
                        row.ConstantItem(100).AlignRight().Text(node.Balance.ToString("N2"));
                    });
                    if (node.Children.Any())
                        ComposePdfTree(col, node.Children, level + 1);
                }
            }

            var pdf = document.GeneratePdf();
            return File(pdf, "application/pdf", "BalanceSheet.pdf");
        }

        // GET: Reports/BalanceSheetExcel
        public async Task<IActionResult> BalanceSheetExcel(int? branchId, DateTime? asOfDate, bool includePending = false)
        {
            var model = await BuildBalanceSheetViewModel(branchId, asOfDate ?? DateTime.Now, includePending);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet("BalanceSheet");
            var row = 1;
            worksheet.Cell(row, 1).Value = "الحساب";
            worksheet.Cell(row, 2).Value = "الرصيد";
            row++;

            void WriteNodes(List<AccountTreeNodeViewModel> nodes, int level)
            {
                foreach (var node in nodes)
                {
                    worksheet.Cell(row, 1).Value = new string(' ', level * 2) + (node.Id == 0 ? node.NameAr : $"{node.Code} - {node.NameAr}");
                    worksheet.Cell(row, 2).Value = node.Balance;
                    row++;
                    if (node.Children.Any())
                        WriteNodes(node.Children, level + 1);
                }
            }

            WriteNodes(model.Assets, 0);
            worksheet.Cell(row, 1).Value = "إجمالي الأصول";
            worksheet.Cell(row, 2).Value = model.TotalAssets;
            row++;
            WriteNodes(model.Liabilities, 0);
            worksheet.Cell(row, 1).Value = "إجمالي الخصوم";
            worksheet.Cell(row, 2).Value = model.TotalLiabilities;
            row++;
            WriteNodes(model.Equity, 0);
            worksheet.Cell(row, 1).Value = "إجمالي حقوق الملكية";
            worksheet.Cell(row, 2).Value = model.TotalEquity;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "BalanceSheet.xlsx");
        }

        private async Task<BalanceSheetViewModel> BuildBalanceSheetViewModel(int? branchId, DateTime asOfDate, bool includePending)
        {
            var accounts = await _context.Accounts
                .Include(a => a.JournalEntryLines)
                    .ThenInclude(l => l.JournalEntry)
                .Where(a => a.Classification == AccountClassification.BalanceSheet)
                .Where(a => !branchId.HasValue || a.BranchId == branchId || a.BranchId == null)
                .AsNoTracking()
                .ToListAsync();

            var balances = accounts.ToDictionary(a => a.Id, a =>
                a.OpeningBalance + a.JournalEntryLines
                    .Where(l => includePending || l.JournalEntry.Status == JournalEntryStatus.Posted)
                    .Where(l => l.JournalEntry.Date <= asOfDate)
                    .Where(l => !branchId.HasValue || l.JournalEntry.BranchId == branchId)
                    .Sum(l => l.DebitAmount - l.CreditAmount));

            var nodes = accounts.Select(a => new AccountTreeNodeViewModel
            {
                Id = a.Id,
                Code = a.Code,
                NameAr = a.NameAr,
                AccountType = a.AccountType,
                Nature = a.Nature,
                OpeningBalance = a.OpeningBalance,
                Balance = balances[a.Id],
                IsActive = a.IsActive,
                CanPostTransactions = a.CanPostTransactions,
                ParentId = a.ParentId,
                Level = a.Level,
                Children = new List<AccountTreeNodeViewModel>(),
                HasChildren = false
            }).ToDictionary(n => n.Id);

            foreach (var node in nodes.Values)
            {
                if (node.ParentId.HasValue && nodes.TryGetValue(node.ParentId.Value, out var parent))
                {
                    parent.Children.Add(node);
                    parent.HasChildren = true;
                }
            }

            decimal ComputeBalance(AccountTreeNodeViewModel node)
            {
                if (node.Children.Any())
                {
                    node.Balance = node.Children.Sum(ComputeBalance);
                }
                return node.Balance;
            }

            var rootNodes = nodes.Values.Where(n => n.ParentId == null).ToList();
            foreach (var root in rootNodes)
            {
                ComputeBalance(root);
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
                Branches = await GetBranchesSelectList()
            };

            viewModel.TotalAssets = assets.Sum(a => a.Balance);
            viewModel.TotalLiabilities = liabilities.Sum(l => l.Balance);
            viewModel.TotalEquity = equity.Sum(e => e.Balance);
            viewModel.IsBalanced = viewModel.TotalAssets == (viewModel.TotalLiabilities + viewModel.TotalEquity);

            return viewModel;
        }

        private async Task<IncomeStatementViewModel> BuildIncomeStatementViewModel(int? branchId, DateTime fromDate, DateTime toDate, bool includePending)
        {
            var accounts = await _context.Accounts
                .Include(a => a.JournalEntryLines)
                    .ThenInclude(l => l.JournalEntry)
                .Where(a => a.Classification == AccountClassification.IncomeStatement)
                .Where(a => !branchId.HasValue || a.BranchId == branchId || a.BranchId == null)
                .AsNoTracking()
                .ToListAsync();

            var balances = accounts.ToDictionary(a => a.Id, a =>
                a.JournalEntryLines
                    .Where(l => includePending || l.JournalEntry.Status == JournalEntryStatus.Posted)
                    .Where(l => l.JournalEntry.Date >= fromDate && l.JournalEntry.Date <= toDate)
                    .Where(l => !branchId.HasValue || l.JournalEntry.BranchId == branchId)
                    .Sum(l => a.Nature == AccountNature.Debit ? l.DebitAmount - l.CreditAmount : l.CreditAmount - l.DebitAmount));

            var nodes = accounts.Select(a => new AccountTreeNodeViewModel
            {
                Id = a.Id,
                Code = a.Code,
                NameAr = a.NameAr,
                AccountType = a.AccountType,
                Nature = a.Nature,
                Balance = balances[a.Id],
                ParentId = a.ParentId,
                Level = a.Level,
                Children = new List<AccountTreeNodeViewModel>(),
                HasChildren = false
            }).ToDictionary(n => n.Id);

            foreach (var node in nodes.Values)
            {
                if (node.ParentId.HasValue && nodes.TryGetValue(node.ParentId.Value, out var parent))
                {
                    parent.Children.Add(node);
                    parent.HasChildren = true;
                }
            }

            decimal ComputeBalance(AccountTreeNodeViewModel node)
            {
                if (node.Children.Any())
                {
                    node.Balance = node.Children.Sum(ComputeBalance);
                }
                return node.Balance;
            }

            var rootNodes = nodes.Values.Where(n => n.ParentId == null).ToList();
            foreach (var root in rootNodes)
            {
                ComputeBalance(root);
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
                Branches = await GetBranchesSelectList()
            };

            viewModel.TotalRevenues = revenues.Sum(r => r.Balance);
            viewModel.TotalExpenses = expenses.Sum(e => e.Balance);
            viewModel.NetIncome = viewModel.TotalRevenues - viewModel.TotalExpenses;

            return viewModel;
        }

        // GET: Reports/IncomeStatement
        public async Task<IActionResult> IncomeStatement(int? branchId, DateTime? fromDate, DateTime? toDate, bool includePending = false)
        {
            var model = await BuildIncomeStatementViewModel(
                branchId,
                fromDate ?? DateTime.Now.AddMonths(-1),
                toDate ?? DateTime.Now,
                includePending);
            return View(model);
        }

        // GET: Reports/IncomeStatementPdf
        public async Task<IActionResult> IncomeStatementPdf(int? branchId, DateTime? fromDate, DateTime? toDate, bool includePending = false)
        {
            var model = await BuildIncomeStatementViewModel(
                branchId,
                fromDate ?? DateTime.Now.AddMonths(-1),
                toDate ?? DateTime.Now,
                includePending);

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
                        ComposePdfTree(col, model.Revenues, 0);
                        col.Item().Text($"إجمالي الإيرادات: {model.TotalRevenues:N2}");

                        col.Item().PaddingTop(10).Text("المصروفات").FontSize(14).Bold();
                        ComposePdfTree(col, model.Expenses, 0);
                        col.Item().Text($"إجمالي المصروفات: {model.TotalExpenses:N2}");

                        col.Item().PaddingTop(10).Text($"صافي الدخل: {model.NetIncome:N2}").FontSize(14).Bold();
                    });
                });
            });

            static void ComposePdfTree(ColumnDescriptor col, List<AccountTreeNodeViewModel> nodes, int level)
            {
                foreach (var node in nodes)
                {
                    col.Item().Row(row =>
                    {
                        row.ConstantItem(level * 15);
                        row.RelativeItem().Text(node.Id == 0 ? node.NameAr : $"{node.Code} - {node.NameAr}");
                        row.ConstantItem(100).AlignRight().Text(node.Balance.ToString("N2"));
                    });
                    if (node.Children.Any())
                        ComposePdfTree(col, node.Children, level + 1);
                }
            }

            var pdf = document.GeneratePdf();
            return File(pdf, "application/pdf", "IncomeStatement.pdf");
        }

        // GET: Reports/IncomeStatementExcel
        public async Task<IActionResult> IncomeStatementExcel(int? branchId, DateTime? fromDate, DateTime? toDate, bool includePending = false)
        {
            var model = await BuildIncomeStatementViewModel(
                branchId,
                fromDate ?? DateTime.Now.AddMonths(-1),
                toDate ?? DateTime.Now,
                includePending);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet("IncomeStatement");
            var row = 1;
            worksheet.Cell(row, 1).Value = "الحساب";
            worksheet.Cell(row, 2).Value = "المبلغ";
            row++;

            void WriteNodes(List<AccountTreeNodeViewModel> nodes, int level)
            {
                foreach (var node in nodes)
                {
                    worksheet.Cell(row, 1).Value = new string(' ', level * 2) + (node.Id == 0 ? node.NameAr : $"{node.Code} - {node.NameAr}");
                    worksheet.Cell(row, 2).Value = node.Balance;
                    row++;
                    if (node.Children.Any())
                        WriteNodes(node.Children, level + 1);
                }
            }

            WriteNodes(model.Revenues, 0);
            worksheet.Cell(row, 1).Value = "إجمالي الإيرادات";
            worksheet.Cell(row, 2).Value = model.TotalRevenues;
            row++;
            WriteNodes(model.Expenses, 0);
            worksheet.Cell(row, 1).Value = "إجمالي المصروفات";
            worksheet.Cell(row, 2).Value = model.TotalExpenses;
            row++;
            worksheet.Cell(row, 1).Value = "صافي الدخل";
            worksheet.Cell(row, 2).Value = model.NetIncome;

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", "IncomeStatement.xlsx");
        }

        // GET: Reports/AccountStatement
        public async Task<IActionResult> AccountStatement(int? accountId, int? branchId, DateTime? fromDate, DateTime? toDate)
        {
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
                Branches = await GetBranchesSelectList()
            };

            if (accountId.HasValue)
            {
                var account = await _context.Accounts.FindAsync(accountId.Value);
                if (account != null)
                {
                    viewModel.AccountId = accountId;
                    viewModel.AccountCode = account.Code;
                    viewModel.AccountName = account.NameAr;

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
                    foreach (var line in lines)
                    {
                        running += account.Nature == AccountNature.Debit
                            ? line.DebitAmount - line.CreditAmount
                            : line.CreditAmount - line.DebitAmount;
                        viewModel.Transactions.Add(new AccountTransactionViewModel
                        {
                            Date = line.JournalEntry.Date,
                            JournalEntryNumber = line.JournalEntry.Number,
                            Description = line.Description ?? string.Empty,
                            DebitAmount = line.DebitAmount,
                            CreditAmount = line.CreditAmount,
                            RunningBalance = running
                        });
                    }

                    viewModel.OpeningBalance = account.OpeningBalance;
                    viewModel.ClosingBalance = running;
                    viewModel.TotalDebit = viewModel.Transactions.Sum(t => t.DebitAmount);
                    viewModel.TotalCredit = viewModel.Transactions.Sum(t => t.CreditAmount);
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
