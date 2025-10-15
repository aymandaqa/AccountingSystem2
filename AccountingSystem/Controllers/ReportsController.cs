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
using System.Globalization;
using System.Text.Json;
using System.Text.Json.Serialization;
using System.Linq.Dynamic.Core;
using System.Reflection;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading;

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

        private static readonly string[] InventoryKeywordsAr = new[] { "مخزون", "بضاعة", "مواد", "سلع" };
        private static readonly string[] InventoryKeywordsEn = new[] { "inventory", "stock", "goods", "materials" };
        private static readonly string[] ReceivableKeywordsAr = new[] { "ذمم", "مدين", "عملاء" };
        private static readonly string[] ReceivableKeywordsEn = new[] { "receivable", "customer", "clients" };
        private static readonly string[] PayableKeywordsAr = new[] { "مورد", "دائن", "ذمم دائنة" };
        private static readonly string[] PayableKeywordsEn = new[] { "payable", "supplier", "vendors" };
        private static readonly string[] CostOfSalesKeywordsAr = new[] { "تكلفة", "مشتريات", "انتاج", "تصنيع" };
        private static readonly string[] CostOfSalesKeywordsEn = new[] { "cost", "purchase", "production", "manufacturing" };

        private sealed class ExecutiveLineData
        {
            public DateTime Date { get; set; }
            public AccountType AccountType { get; set; }
            public AccountSubClassification AccountSubClassification { get; set; }
            public string AccountNameAr { get; set; } = string.Empty;
            public string? AccountNameEn { get; set; }
            public string AccountCode { get; set; } = string.Empty;
            public decimal Debit { get; set; }
            public decimal Credit { get; set; }
        }

        private static bool ContainsKeyword(string? value, string[] keywords)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return false;
            }

            var normalized = value
                .Replace("أ", "ا")
                .Replace("إ", "ا")
                .Replace("آ", "ا")
                .ToLowerInvariant();

            foreach (var keyword in keywords)
            {
                if (normalized.Contains(keyword))
                {
                    return true;
                }
            }

            return false;
        }

        private static bool IsInventoryAccount(ExecutiveLineData line)
        {
            if (line.AccountType != AccountType.Assets)
            {
                return false;
            }

            return ContainsKeyword(line.AccountNameAr, InventoryKeywordsAr)
                || ContainsKeyword(line.AccountNameEn, InventoryKeywordsEn)
                || line.AccountCode.StartsWith("14", StringComparison.Ordinal)
                || line.AccountCode.StartsWith("15", StringComparison.Ordinal);
        }

        private static bool IsReceivableAccount(ExecutiveLineData line)
        {
            if (line.AccountType != AccountType.Assets)
            {
                return false;
            }

            return ContainsKeyword(line.AccountNameAr, ReceivableKeywordsAr)
                || ContainsKeyword(line.AccountNameEn, ReceivableKeywordsEn)
                || line.AccountCode.StartsWith("12", StringComparison.Ordinal);
        }

        private static bool IsPayableAccount(ExecutiveLineData line)
        {
            if (line.AccountType != AccountType.Liabilities)
            {
                return false;
            }

            return ContainsKeyword(line.AccountNameAr, PayableKeywordsAr)
                || ContainsKeyword(line.AccountNameEn, PayableKeywordsEn)
                || line.AccountCode.StartsWith("21", StringComparison.Ordinal)
                || line.AccountCode.StartsWith("22", StringComparison.Ordinal);
        }

        private static bool IsCostOfSalesAccount(ExecutiveLineData line)
        {
            if (line.AccountType != AccountType.Expenses)
            {
                return false;
            }

            return ContainsKeyword(line.AccountNameAr, CostOfSalesKeywordsAr)
                || ContainsKeyword(line.AccountNameEn, CostOfSalesKeywordsEn)
                || line.AccountCode.StartsWith("51", StringComparison.Ordinal)
                || line.AccountCode.StartsWith("52", StringComparison.Ordinal);
        }

        // GET: Reports
        public IActionResult Index()
        {
            return View();
        }

        public async Task<IActionResult> BranchPerformanceSummary(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var model = await BuildBranchPerformanceSummaryViewModel(fromDate, toDate);
            return View(model);
        }

        public async Task<IActionResult> BranchPerformanceSummaryExcel(DateTime? fromDate = null, DateTime? toDate = null)
        {
            var model = await BuildBranchPerformanceSummaryViewModel(fromDate, toDate);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.AddWorksheet("BranchSummary");

            if (!model.HasResults)
            {
                worksheet.Cell(1, 1).Value = "لا توجد بيانات للفترة المحددة.";
            }
            else
            {
                var headerRow = 1;
                worksheet.Cell(headerRow, 1).Value = "البند";
                var columnIndex = 2;
                foreach (var branch in model.Branches)
                {
                    worksheet.Cell(headerRow, columnIndex).Value = branch.BranchName;
                    columnIndex++;
                }
                worksheet.Cell(headerRow, columnIndex).Value = $"الإجمالي ({model.BaseCurrencyCode})";
                headerRow++;

                var currentRow = headerRow;
                foreach (var section in model.Sections)
                {
                    worksheet.Cell(currentRow, 1).Value = section.Title;
                    worksheet.Range(currentRow, 1, currentRow, model.Branches.Count + 2).Merge();
                    worksheet.Row(currentRow).Style.Font.Bold = true;
                    currentRow++;

                    foreach (var row in section.Rows)
                    {
                        worksheet.Cell(currentRow, 1).Value = row.Label;
                        columnIndex = 2;
                        foreach (var branch in model.Branches)
                        {
                            var value = row.Values.TryGetValue(branch.BranchId, out var amount) ? amount : 0m;
                            worksheet.Cell(currentRow, columnIndex).Value = Math.Round(value, 2, MidpointRounding.AwayFromZero);
                            columnIndex++;
                        }

                        worksheet.Cell(currentRow, columnIndex).Value = Math.Round(row.Total, 2, MidpointRounding.AwayFromZero);
                        currentRow++;
                    }

                    worksheet.Cell(currentRow, 1).Value = $"إجمالي {section.Title}";
                    worksheet.Row(currentRow).Style.Font.Bold = true;
                    columnIndex = 2;
                    foreach (var branch in model.Branches)
                    {
                        var value = section.TotalsByBranch.TryGetValue(branch.BranchId, out var amount) ? amount : 0m;
                        worksheet.Cell(currentRow, columnIndex).Value = Math.Round(value, 2, MidpointRounding.AwayFromZero);
                        columnIndex++;
                    }
                    worksheet.Cell(currentRow, columnIndex).Value = Math.Round(section.OverallTotal, 2, MidpointRounding.AwayFromZero);
                    currentRow++;
                }

                if (model.SummaryRows.Any())
                {
                    worksheet.Cell(currentRow, 1).Value = "مؤشرات الأداء";
                    worksheet.Range(currentRow, 1, currentRow, model.Branches.Count + 2).Merge();
                    worksheet.Row(currentRow).Style.Font.Bold = true;
                    currentRow++;

                    foreach (var row in model.SummaryRows)
                    {
                        worksheet.Cell(currentRow, 1).Value = row.Label;
                        columnIndex = 2;
                        foreach (var branch in model.Branches)
                        {
                            var value = row.Values.TryGetValue(branch.BranchId, out var amount) ? amount : 0m;
                            worksheet.Cell(currentRow, columnIndex).Value = Math.Round(value, 2, MidpointRounding.AwayFromZero);
                            columnIndex++;
                        }
                        worksheet.Cell(currentRow, columnIndex).Value = Math.Round(row.Total, 2, MidpointRounding.AwayFromZero);
                        currentRow++;
                    }
                }

                worksheet.Columns().AdjustToContents();
            }

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            var content = stream.ToArray();
            var fileName = $"BranchPerformanceSummary_{model.FromDate:yyyyMMdd}_{model.ToDate:yyyyMMdd}.xlsx";
            return File(content, "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [Authorize(Policy = "reports.view")]
        public async Task<IActionResult> ExecutiveDashboard(int? year, int? month)
        {
            var baseCurrency = await _context.Currencies.AsNoTracking().FirstAsync(c => c.IsBase);
            var today = DateTime.Today;

            var availableYears = await _context.JournalEntries
                .AsNoTracking()
                .Select(e => e.Date.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            if (!availableYears.Any())
            {
                availableYears.Add(today.Year);
            }

            var selectedYear = year.HasValue && availableYears.Contains(year.Value)
                ? year.Value
                : availableYears.First();

            var availableMonths = await _context.JournalEntries
                .AsNoTracking()
                .Where(e => e.Date.Year == selectedYear)
                .Select(e => e.Date.Month)
                .Distinct()
                .OrderBy(m => m)
                .ToListAsync();

            if (!availableMonths.Any())
            {
                availableMonths = Enumerable.Range(1, 12).ToList();
            }

            var selectedMonth = month.HasValue && availableMonths.Contains(month.Value)
                ? month.Value
                : availableMonths.Max();

            if (selectedYear == today.Year && selectedMonth > today.Month)
            {
                selectedMonth = today.Month;
            }

            if (selectedMonth < 1)
            {
                selectedMonth = 1;
            }

            var monthStart = new DateTime(selectedYear, selectedMonth, 1);
            var monthEnd = monthStart.AddMonths(1);
            var yearStart = new DateTime(selectedYear, 1, 1);
            var yearEnd = monthEnd;

            var previousMonthStart = monthStart.AddYears(-1);
            var previousMonthEnd = previousMonthStart.AddMonths(1);
            var previousYearStart = yearStart.AddYears(-1);
            var previousYearEnd = yearEnd.AddYears(-1);

            var linesRaw = await _context.JournalEntryLines
                .AsNoTracking()
                .Where(l => l.JournalEntry.Date >= previousYearStart && l.JournalEntry.Date < yearEnd)
                .Select(l => new
                {
                    l.JournalEntry.Date,
                    l.DebitAmount,
                    l.CreditAmount,
                    l.Account.AccountType,
                    l.Account.SubClassification,
                    l.Account.NameAr,
                    l.Account.NameEn,
                    l.Account.Code,
                    CurrencyId = l.Account.CurrencyId
                })
                .ToListAsync();

            var currencyIds = linesRaw.Select(l => l.CurrencyId).Distinct().ToList();
            if (!currencyIds.Contains(baseCurrency.Id))
            {
                currencyIds.Add(baseCurrency.Id);
            }

            var currencies = await _context.Currencies
                .AsNoTracking()
                .Where(c => currencyIds.Contains(c.Id))
                .ToDictionaryAsync(c => c.Id);

            decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);
            decimal SafeDivide(decimal numerator, decimal denominator) => denominator == 0m ? 0m : numerator / denominator;

            var lines = linesRaw.Select(l =>
            {
                var currency = currencies.TryGetValue(l.CurrencyId, out var c) ? c : baseCurrency;
                return new ExecutiveLineData
                {
                    Date = l.Date,
                    AccountType = l.AccountType,
                    AccountSubClassification = l.SubClassification,
                    AccountNameAr = l.NameAr ?? string.Empty,
                    AccountNameEn = l.NameEn,
                    AccountCode = l.Code ?? string.Empty,
                    Debit = _currencyService.Convert(l.DebitAmount, currency, baseCurrency),
                    Credit = _currencyService.Convert(l.CreditAmount, currency, baseCurrency)
                };
            }).ToList();

            var monthLines = lines.Where(l => l.Date >= monthStart && l.Date < monthEnd).ToList();
            var yearToDateLines = lines.Where(l => l.Date >= yearStart && l.Date < yearEnd).ToList();
            var previousMonthLines = lines.Where(l => l.Date >= previousMonthStart && l.Date < previousMonthEnd).ToList();
            var previousYearToDateLines = lines.Where(l => l.Date >= previousYearStart && l.Date < previousYearEnd).ToList();

            decimal SumRevenue(IEnumerable<ExecutiveLineData> source) => source.Where(l => l.AccountType == AccountType.Revenue).Sum(l => l.Credit - l.Debit);
            decimal SumCostOfSales(IEnumerable<ExecutiveLineData> source) => source.Where(IsCostOfSalesAccount).Sum(l => l.Debit - l.Credit);
            decimal SumOperatingExpenses(IEnumerable<ExecutiveLineData> source) => source.Where(l => l.AccountType == AccountType.Expenses && !IsCostOfSalesAccount(l)).Sum(l => l.Debit - l.Credit);
            decimal SumReceivables(IEnumerable<ExecutiveLineData> source) => source.Where(IsReceivableAccount).Sum(l => l.Debit - l.Credit);
            decimal SumInventory(IEnumerable<ExecutiveLineData> source) => source.Where(IsInventoryAccount).Sum(l => l.Debit - l.Credit);
            decimal SumPayables(IEnumerable<ExecutiveLineData> source) => source.Where(IsPayableAccount).Sum(l => l.Credit - l.Debit);

            var revenueMonth = SumRevenue(monthLines);
            var revenueMonthTarget = SumRevenue(previousMonthLines);
            var revenueYearToDate = SumRevenue(yearToDateLines);
            var revenueYearToDateTarget = SumRevenue(previousYearToDateLines);

            var costOfSalesMonth = SumCostOfSales(monthLines);
            var costOfSalesMonthTarget = SumCostOfSales(previousMonthLines);
            var costOfSalesYearToDate = SumCostOfSales(yearToDateLines);
            var costOfSalesYearToDateTarget = SumCostOfSales(previousYearToDateLines);

            var operatingExpensesMonth = SumOperatingExpenses(monthLines);
            var operatingExpensesMonthTarget = SumOperatingExpenses(previousMonthLines);
            var operatingExpensesYearToDate = SumOperatingExpenses(yearToDateLines);
            var operatingExpensesYearToDateTarget = SumOperatingExpenses(previousYearToDateLines);

            var grossProfitMonth = revenueMonth - costOfSalesMonth;
            var grossProfitMonthTarget = revenueMonthTarget - costOfSalesMonthTarget;
            var grossProfitYearToDate = revenueYearToDate - costOfSalesYearToDate;
            var grossProfitYearToDateTarget = revenueYearToDateTarget - costOfSalesYearToDateTarget;

            var operatingProfitMonth = grossProfitMonth - operatingExpensesMonth;
            var operatingProfitMonthTarget = grossProfitMonthTarget - operatingExpensesMonthTarget;
            var operatingProfitYearToDate = grossProfitYearToDate - operatingExpensesYearToDate;
            var operatingProfitYearToDateTarget = grossProfitYearToDateTarget - operatingExpensesYearToDateTarget;

            var netProfitBeforeTaxMonth = operatingProfitMonth;
            var netProfitBeforeTaxMonthTarget = operatingProfitMonthTarget;
            var netProfitBeforeTaxYearToDate = operatingProfitYearToDate;
            var netProfitBeforeTaxYearToDateTarget = operatingProfitYearToDateTarget;

            var receivablesMonth = SumReceivables(monthLines);
            var receivablesMonthTarget = SumReceivables(previousMonthLines);
            var receivablesYearToDate = SumReceivables(yearToDateLines);
            var receivablesYearToDateTarget = SumReceivables(previousYearToDateLines);

            var inventoryMonth = SumInventory(monthLines);
            var inventoryMonthTarget = SumInventory(previousMonthLines);
            var inventoryYearToDate = SumInventory(yearToDateLines);
            var inventoryYearToDateTarget = SumInventory(previousYearToDateLines);

            var payablesMonth = SumPayables(monthLines);
            var payablesYearToDate = SumPayables(yearToDateLines);

            decimal Margin(decimal profit, decimal revenue) => revenue == 0m ? 0m : (profit / revenue) * 100m;

            var netProfitMarginMonth = Margin(netProfitBeforeTaxMonth, revenueMonth);
            var netProfitMarginMonthTarget = Margin(netProfitBeforeTaxMonthTarget, revenueMonthTarget);
            var netProfitMarginYearToDate = Margin(netProfitBeforeTaxYearToDate, revenueYearToDate);
            var netProfitMarginYearToDateTarget = Margin(netProfitBeforeTaxYearToDateTarget, revenueYearToDateTarget);

            var operatingProfitMarginMonth = Margin(operatingProfitMonth, revenueMonth);
            var operatingProfitMarginMonthTarget = Margin(operatingProfitMonthTarget, revenueMonthTarget);
            var operatingProfitMarginYearToDate = Margin(operatingProfitYearToDate, revenueYearToDate);
            var operatingProfitMarginYearToDateTarget = Margin(operatingProfitYearToDateTarget, revenueYearToDateTarget);

            var daysInMonth = (decimal)(monthEnd - monthStart).TotalDays;
            var daysInYear = (decimal)(yearEnd - yearStart).TotalDays;

            var baseForInventoryMonth = Math.Abs(costOfSalesMonth) > 0m ? Math.Abs(costOfSalesMonth) : Math.Abs(revenueMonth);
            var baseForInventoryYear = Math.Abs(costOfSalesYearToDate) > 0m ? Math.Abs(costOfSalesYearToDate) : Math.Abs(revenueYearToDate);

            var dsoMonth = SafeDivide(Math.Abs(receivablesMonth), Math.Abs(revenueMonth)) * daysInMonth;
            var dsoYear = SafeDivide(Math.Abs(receivablesYearToDate), Math.Abs(revenueYearToDate)) * daysInYear;

            var dioMonth = baseForInventoryMonth == 0m ? 0m : SafeDivide(Math.Abs(inventoryMonth), baseForInventoryMonth) * daysInMonth;
            var dioYear = baseForInventoryYear == 0m ? 0m : SafeDivide(Math.Abs(inventoryYearToDate), baseForInventoryYear) * daysInYear;

            var dpoMonth = baseForInventoryMonth == 0m ? 0m : SafeDivide(Math.Abs(payablesMonth), baseForInventoryMonth) * daysInMonth;
            var dpoYear = baseForInventoryYear == 0m ? 0m : SafeDivide(Math.Abs(payablesYearToDate), baseForInventoryYear) * daysInYear;

            var cashConversionCycleMonth = Round(dioMonth + dsoMonth - dpoMonth);
            var cashConversionCycleYear = Round(dioYear + dsoYear - dpoYear);

            ExecutiveDashboardMetric CreateMetric(string title, decimal actual, decimal target, bool isPercentage = false, string? tooltip = null)
            {
                return new ExecutiveDashboardMetric
                {
                    Title = title,
                    Actual = Round(actual),
                    Target = Round(target),
                    Unit = isPercentage ? "%" : baseCurrency.Code,
                    IsPercentage = isPercentage,
                    Tooltip = tooltip
                };
            }

            var monthlyMetrics = new List<ExecutiveDashboardMetric>
            {
                CreateMetric("الإيرادات", revenueMonth, revenueMonthTarget),
                CreateMetric("مجمل الربح", grossProfitMonth, grossProfitMonthTarget),
                CreateMetric("الربح التشغيلي (EBIT)", operatingProfitMonth, operatingProfitMonthTarget),
                CreateMetric("صافي الربح قبل الضريبة", netProfitBeforeTaxMonth, netProfitBeforeTaxMonthTarget),
                CreateMetric("المخزون", inventoryMonth, inventoryMonthTarget),
                CreateMetric("الذمم المدينة", receivablesMonth, receivablesMonthTarget),
                CreateMetric("هامش صافي الربح", netProfitMarginMonth, netProfitMarginMonthTarget, true),
                CreateMetric("هامش الربح التشغيلي", operatingProfitMarginMonth, operatingProfitMarginMonthTarget, true)
            };

            var yearToDateMetrics = new List<ExecutiveDashboardMetric>
            {
                CreateMetric("الإيرادات التراكمية", revenueYearToDate, revenueYearToDateTarget),
                CreateMetric("مجمل الربح التراكمي", grossProfitYearToDate, grossProfitYearToDateTarget),
                CreateMetric("الربح التشغيلي التراكمي", operatingProfitYearToDate, operatingProfitYearToDateTarget),
                CreateMetric("صافي الربح قبل الضريبة (تراكمي)", netProfitBeforeTaxYearToDate, netProfitBeforeTaxYearToDateTarget),
                CreateMetric("متوسط المخزون", inventoryYearToDate, inventoryYearToDateTarget),
                CreateMetric("متوسط الذمم المدينة", receivablesYearToDate, receivablesYearToDateTarget),
                CreateMetric("هامش صافي الربح التراكمي", netProfitMarginYearToDate, netProfitMarginYearToDateTarget, true),
                CreateMetric("هامش الربح التشغيلي التراكمي", operatingProfitMarginYearToDate, operatingProfitMarginYearToDateTarget, true)
            };

            var culture = CultureInfo.CreateSpecificCulture("ar-SA");
            var monthlyTrend = Enumerable.Range(1, selectedMonth)
                .Select(m =>
                {
                    var rangeStart = new DateTime(selectedYear, m, 1);
                    var rangeEnd = rangeStart.AddMonths(1);
                    var data = lines.Where(l => l.Date >= rangeStart && l.Date < rangeEnd).ToList();
                    var revenue = Round(SumRevenue(data));
                    var cost = Round(SumCostOfSales(data));
                    var operating = Round(SumOperatingExpenses(data));
                    var profit = Round(revenue - cost - operating);
                    return new ExecutiveDashboardTrendPoint
                    {
                        MonthNumber = m,
                        Label = rangeStart.ToString("MM", culture),
                        Revenue = revenue,
                        CostOfSales = cost,
                        OperatingExpenses = operating,
                        Profit = profit,
                        IsSelected = m == selectedMonth
                    };
                })
                .ToList();

            var expenseGroupsMonth = monthLines
                .Where(l => l.AccountType == AccountType.Expenses)
                .GroupBy(l => string.IsNullOrWhiteSpace(l.AccountNameAr) ? (l.AccountNameEn ?? l.AccountCode) : l.AccountNameAr)
                .Select(g => new { Name = g.Key, Amount = Round(Math.Abs(g.Sum(x => x.Debit - x.Credit))) })
                .Where(x => x.Amount > 0m)
                .ToList();

            var expenseGroupsYear = yearToDateLines
                .Where(l => l.AccountType == AccountType.Expenses)
                .GroupBy(l => string.IsNullOrWhiteSpace(l.AccountNameAr) ? (l.AccountNameEn ?? l.AccountCode) : l.AccountNameAr)
                .Select(g => new { Name = g.Key, Amount = Round(Math.Abs(g.Sum(x => x.Debit - x.Credit))) })
                .Where(x => x.Amount > 0m)
                .ToList();

            var topExpenseNames = expenseGroupsYear
                .OrderByDescending(x => x.Amount)
                .Take(6)
                .Select(x => x.Name)
                .ToList();

            foreach (var item in expenseGroupsMonth.OrderByDescending(x => x.Amount))
            {
                if (topExpenseNames.Count >= 6)
                {
                    break;
                }

                if (!topExpenseNames.Contains(item.Name))
                {
                    topExpenseNames.Add(item.Name);
                }
            }

            var operatingExpenseBreakdown = topExpenseNames
                .Select(name => new OperatingExpenseBreakdownItem
                {
                    Name = name,
                    MonthlyAmount = Round(expenseGroupsMonth.FirstOrDefault(x => x.Name == name)?.Amount ?? 0m),
                    YearToDateAmount = Round(expenseGroupsYear.FirstOrDefault(x => x.Name == name)?.Amount ?? 0m)
                })
                .ToList();

            var monthOther = expenseGroupsMonth.Where(x => !topExpenseNames.Contains(x.Name)).Sum(x => x.Amount);
            var yearOther = expenseGroupsYear.Where(x => !topExpenseNames.Contains(x.Name)).Sum(x => x.Amount);

            if (monthOther > 0m || yearOther > 0m)
            {
                operatingExpenseBreakdown.Add(new OperatingExpenseBreakdownItem
                {
                    Name = "أخرى",
                    MonthlyAmount = Round(monthOther),
                    YearToDateAmount = Round(yearOther)
                });
            }

            var incomeStatement = new List<ExecutiveIncomeStatementRow>
            {
                new ExecutiveIncomeStatementRow
                {
                    Name = "الإيرادات",
                    MonthlyActual = Round(revenueMonth),
                    MonthlyTarget = Round(revenueMonthTarget),
                    YearToDateActual = Round(revenueYearToDate),
                    YearToDateTarget = Round(revenueYearToDateTarget)
                },
                new ExecutiveIncomeStatementRow
                {
                    Name = "تكلفة المبيعات",
                    MonthlyActual = Round(costOfSalesMonth),
                    MonthlyTarget = Round(costOfSalesMonthTarget),
                    YearToDateActual = Round(costOfSalesYearToDate),
                    YearToDateTarget = Round(costOfSalesYearToDateTarget)
                },
                new ExecutiveIncomeStatementRow
                {
                    Name = "مجمل الربح",
                    MonthlyActual = Round(grossProfitMonth),
                    MonthlyTarget = Round(grossProfitMonthTarget),
                    YearToDateActual = Round(grossProfitYearToDate),
                    YearToDateTarget = Round(grossProfitYearToDateTarget)
                },
                new ExecutiveIncomeStatementRow
                {
                    Name = "المصروفات التشغيلية",
                    MonthlyActual = Round(operatingExpensesMonth),
                    MonthlyTarget = Round(operatingExpensesMonthTarget),
                    YearToDateActual = Round(operatingExpensesYearToDate),
                    YearToDateTarget = Round(operatingExpensesYearToDateTarget)
                },
                new ExecutiveIncomeStatementRow
                {
                    Name = "الربح التشغيلي (EBIT)",
                    MonthlyActual = Round(operatingProfitMonth),
                    MonthlyTarget = Round(operatingProfitMonthTarget),
                    YearToDateActual = Round(operatingProfitYearToDate),
                    YearToDateTarget = Round(operatingProfitYearToDateTarget)
                },
                new ExecutiveIncomeStatementRow
                {
                    Name = "صافي الربح قبل الضريبة",
                    MonthlyActual = Round(netProfitBeforeTaxMonth),
                    MonthlyTarget = Round(netProfitBeforeTaxMonthTarget),
                    YearToDateActual = Round(netProfitBeforeTaxYearToDate),
                    YearToDateTarget = Round(netProfitBeforeTaxYearToDateTarget)
                }
            };

            var cashConversionMetrics = new List<CashConversionMetric>
            {
                new CashConversionMetric
                {
                    Name = "مدة دوران المخزون (DIO)",
                    MonthlyValue = Round(dioMonth),
                    YearToDateValue = Round(dioYear)
                },
                new CashConversionMetric
                {
                    Name = "مدة تحصيل الذمم (DSO)",
                    MonthlyValue = Round(dsoMonth),
                    YearToDateValue = Round(dsoYear)
                },
                new CashConversionMetric
                {
                    Name = "مدة سداد الذمم (DPO)",
                    MonthlyValue = Round(dpoMonth),
                    YearToDateValue = Round(dpoYear)
                }
            };

            var viewModel = new ExecutiveDashboardViewModel
            {
                Year = selectedYear,
                Month = selectedMonth,
                YearDisplay = selectedYear.ToString("0000", culture),
                MonthDisplay = monthStart.ToString("MM", culture),
                CurrencyCode = baseCurrency.Code,
                AvailableYears = availableYears,
                AvailableMonths = availableMonths,
                MonthlyMetrics = monthlyMetrics,
                YearToDateMetrics = yearToDateMetrics,
                MonthlyTrend = monthlyTrend,
                CashConversionMetrics = cashConversionMetrics,
                CashConversionCycleMonthly = cashConversionCycleMonth,
                CashConversionCycleYearToDate = cashConversionCycleYear,
                OperatingExpenseBreakdown = operatingExpenseBreakdown,
                IncomeStatement = incomeStatement
            };

            return View(viewModel);
        }

        [Authorize(Policy = "reports.view")]
        public async Task<IActionResult> UserCashTransactions(string? type, int? accountId)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var normalizedType = string.IsNullOrWhiteSpace(type)
                ? "all"
                : type.Trim().ToLowerInvariant();

            if (normalizedType != "payment" && normalizedType != "receipt")
            {
                normalizedType = "all";
            }

            var paymentVouchers = await _context.PaymentVouchers
                .AsNoTracking()
                .Where(v => v.CreatedById == userId)
                .Include(v => v.Currency)
                .Include(v => v.Account)
                .Include(v => v.CreatedBy)
                    .ThenInclude(u => u.PaymentAccount)
                .ToListAsync();

            var receiptVouchers = await _context.ReceiptVouchers
                .AsNoTracking()
                .Where(v => v.CreatedById == userId)
                .Include(v => v.Currency)
                .Include(v => v.Account)
                .Include(v => v.PaymentAccount)
                .ToListAsync();

            var references = new List<string>();
            references.AddRange(paymentVouchers.Select(v => $"PAYV:{v.Id}"));
            references.AddRange(receiptVouchers.Select(v => $"RCV:{v.Id}"));

            var journalEntries = references.Count == 0
                ? new Dictionary<string, JournalEntry>()
                : await _context.JournalEntries
                    .AsNoTracking()
                    .Where(e => e.Reference != null && references.Contains(e.Reference))
                    .ToDictionaryAsync(e => e.Reference!);

            var accountIds = paymentVouchers
                .Select(v => v.IsCash ? v.CreatedBy.PaymentAccountId : v.AccountId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Concat(receiptVouchers.Select(v => v.PaymentAccountId))
                .Distinct()
                .ToList();

            var accounts = accountIds.Count == 0
                ? new Dictionary<int, Account>()
                : await _context.Accounts
                    .AsNoTracking()
                    .Where(a => accountIds.Contains(a.Id))
                    .ToDictionaryAsync(a => a.Id);

            var items = new List<UserCashTransactionReportItem>();

            if (normalizedType != "payment")
            {
                foreach (var receipt in receiptVouchers)
                {
                    if (accountId.HasValue && receipt.PaymentAccountId != accountId.Value)
                    {
                        continue;
                    }

                    var reference = $"RCV:{receipt.Id}";
                    journalEntries.TryGetValue(reference, out var entry);

                    items.Add(new UserCashTransactionReportItem
                    {
                        Date = receipt.Date,
                        Type = CashTransactionType.Receipt,
                        AccountId = receipt.PaymentAccountId,
                        AccountName = accounts.TryGetValue(receipt.PaymentAccountId, out var account)
                            ? account.NameAr
                            : receipt.PaymentAccount?.NameAr ?? "-",
                        Amount = receipt.Amount,
                        Currency = receipt.Currency.Code,
                        Reference = reference,
                        Notes = receipt.Notes,
                        JournalEntryId = entry?.Id,
                        JournalEntryNumber = entry?.Number
                    });
                }
            }

            if (normalizedType != "receipt")
            {
                foreach (var payment in paymentVouchers)
                {
                    var paymentAccountId = payment.IsCash
                        ? payment.CreatedBy.PaymentAccountId
                        : payment.AccountId;

                    if (accountId.HasValue && paymentAccountId != accountId.Value)
                    {
                        continue;
                    }

                    var reference = $"PAYV:{payment.Id}";
                    journalEntries.TryGetValue(reference, out var entry);

                    string accountName = "-";
                    if (paymentAccountId.HasValue && accounts.TryGetValue(paymentAccountId.Value, out var account))
                    {
                        accountName = account.NameAr;
                    }
                    else if (payment.IsCash && payment.CreatedBy.PaymentAccount != null)
                    {
                        accountName = payment.CreatedBy.PaymentAccount.NameAr;
                    }
                    else if (payment.Account != null)
                    {
                        accountName = payment.Account.NameAr;
                    }

                    items.Add(new UserCashTransactionReportItem
                    {
                        Date = payment.Date,
                        Type = CashTransactionType.Payment,
                        AccountId = paymentAccountId,
                        AccountName = accountName,
                        Amount = payment.Amount,
                        Currency = payment.Currency.Code,
                        Reference = reference,
                        Notes = payment.Notes,
                        JournalEntryId = entry?.Id,
                        JournalEntryNumber = entry?.Number
                    });
                }
            }

            var orderedItems = items
                .OrderByDescending(i => i.Date)
                .ThenBy(i => i.Reference)
                .ToList();

            var accountOptions = accounts.Values
                .OrderBy(a => a.NameAr)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}",
                    Selected = accountId.HasValue && a.Id == accountId.Value
                })
                .ToList();

            var viewModel = new UserCashTransactionReportViewModel
            {
                SelectedType = normalizedType,
                SelectedAccountId = accountId,
                Accounts = accountOptions,
                Items = orderedItems
            };

            return View(viewModel);
        }

        [Authorize(Policy = "reports.view")]
        public async Task<IActionResult> UserDailyJournalEntries(DateTime? fromDate, DateTime? toDate, string? reference)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Challenge();
            }

            var today = DateTime.Today;
            var startDate = (fromDate ?? today).Date;
            var endDate = (toDate ?? today).Date;

            if (endDate < startDate)
            {
                endDate = startDate;
            }

            var referenceFilter = string.IsNullOrWhiteSpace(reference)
                ? null
                : reference!.Trim();

            var query = _context.JournalEntries
                .AsNoTracking()
                .Where(e => e.CreatedById == userId)
                .Where(e => e.Date >= startDate && e.Date <= endDate);

            if (!string.IsNullOrEmpty(referenceFilter))
            {
                query = query.Where(e => e.Reference != null && EF.Functions.Like(e.Reference, $"%{referenceFilter}%"));
            }

            var entries = await query
                .Select(e => new
                {
                    e.Id,
                    e.Number,
                    e.Date,
                    e.Description,
                    e.Reference,
                    e.TotalDebit,
                    e.TotalCredit
                })
                .ToListAsync();

            var grouped = entries
                .GroupBy(e => new
                {
                    Date = e.Date.Date,
                    Reference = string.IsNullOrWhiteSpace(e.Reference) ? "بدون مرجع" : e.Reference!.Trim()
                })
                .OrderByDescending(g => g.Key.Date)
                .ThenBy(g => g.Key.Reference, StringComparer.OrdinalIgnoreCase)
                .Select(g => new UserJournalEntryDailyReportItem
                {
                    Date = g.Key.Date,
                    Reference = g.Key.Reference,
                    TotalDebit = g.Sum(x => x.TotalDebit),
                    TotalCredit = g.Sum(x => x.TotalCredit),
                    Entries = g
                        .OrderByDescending(x => x.Date)
                        .ThenBy(x => x.Number, StringComparer.OrdinalIgnoreCase)
                        .Select(x => new UserJournalEntrySummary
                        {
                            JournalEntryId = x.Id,
                            Number = x.Number,
                            Description = x.Description,
                            TotalDebit = x.TotalDebit,
                            TotalCredit = x.TotalCredit
                        })
                        .ToList()
                })
                .ToList();

            var viewModel = new UserJournalEntryDailyReportViewModel
            {
                FromDate = startDate,
                ToDate = endDate,
                ReferenceFilter = referenceFilter,
                Items = grouped
            };

            return View(viewModel);
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
        public IActionResult QueryBuilder()
        {
            var viewModel = new QueryBuilderReportViewModel
            {
                Datasets = QueryBuilderDatasets.All
                    .Select(d => new SelectListItem
                    {
                        Value = d.Key,
                        Text = d.Name
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
        public IActionResult GetQueryDatasets()
        {
            var datasets = QueryBuilderDatasets.All
                .Select(dataset => new QueryDatasetInfoViewModel
                {
                    Key = dataset.Key,
                    Name = dataset.Name,
                    Description = dataset.Description,
                    Fields = dataset.Fields
                        .Select(f => new QueryDatasetFieldViewModel
                        {
                            Field = f.Field,
                            Label = f.Label,
                            Type = GetFieldTypeString(f.FieldType),
                            Category = f.Category
                        })
                        .ToList()
                })
                .ToList();

            return Json(datasets);
        }

        [Authorize(Policy = "reports.dynamic")]
        [HttpGet]
        public IActionResult GetQueryDataset(string key)
        {
            var dataset = QueryBuilderDatasets.GetByKey(key);
            if (dataset == null)
            {
                return NotFound();
            }

            var response = new QueryDatasetInfoViewModel
            {
                Key = dataset.Key,
                Name = dataset.Name,
                Description = dataset.Description,
                Fields = dataset.Fields
                    .Select(f => new QueryDatasetFieldViewModel
                    {
                        Field = f.Field,
                        Label = f.Label,
                        Type = GetFieldTypeString(f.FieldType),
                        Category = f.Category
                    })
                    .ToList()
            };

            return Json(response);
        }

        [Authorize(Policy = "reports.dynamic")]
        [HttpGet]
        public async Task<IActionResult> GetReportQueries(string? datasetKey = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var query = _context.ReportQueries
                .AsNoTracking()
                .Where(r => r.CreatedById == userId);

            if (!string.IsNullOrEmpty(datasetKey))
            {
                query = query.Where(r => r.DatasetKey == datasetKey);
            }

            var items = await query
                .OrderByDescending(r => r.UpdatedAt ?? r.CreatedAt)
                .Select(r => new ReportQueryListItemViewModel
                {
                    Id = r.Id,
                    Name = r.Name,
                    DatasetKey = r.DatasetKey,
                    UpdatedAt = r.UpdatedAt ?? r.CreatedAt
                })
                .ToListAsync();

            return Json(items);
        }

        [Authorize(Policy = "reports.dynamic")]
        [HttpGet]
        public async Task<IActionResult> GetReportQuery(int id)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var query = await _context.ReportQueries
                .AsNoTracking()
                .FirstOrDefaultAsync(r => r.Id == id && r.CreatedById == userId);

            if (query == null)
            {
                return NotFound();
            }

            return Json(new
            {
                query.Id,
                query.Name,
                query.DatasetKey,
                query.RulesJson,
                query.SelectedColumnsJson
            });
        }

        [Authorize(Policy = "reports.dynamic")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> SaveReportQuery([FromBody] SaveReportQueryRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var dataset = QueryBuilderDatasets.GetByKey(request.DatasetKey);
            if (dataset == null)
            {
                return BadRequest(new { message = "مجموعة البيانات المحددة غير موجودة." });
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            ReportQuery entity;
            if (request.Id.HasValue && request.Id.Value > 0)
            {
                entity = await _context.ReportQueries
                    .FirstOrDefaultAsync(r => r.Id == request.Id.Value && r.CreatedById == userId);

                if (entity == null)
                {
                    return NotFound(new { message = "لم يتم العثور على التقرير المطلوب تحديثه." });
                }
            }
            else
            {
                entity = new ReportQuery
                {
                    CreatedById = userId,
                    CreatedAt = DateTime.UtcNow
                };
                _context.ReportQueries.Add(entity);
            }

            entity.Name = request.Name.Trim();
            entity.DatasetKey = dataset.Key;
            entity.RulesJson = request.RulesJson;
            entity.SelectedColumnsJson = string.IsNullOrWhiteSpace(request.SelectedColumnsJson)
                ? null
                : request.SelectedColumnsJson;
            entity.UpdatedAt = DateTime.UtcNow;

            await _context.SaveChangesAsync();

            return Json(new { entity.Id });
        }

        [Authorize(Policy = "reports.dynamic")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> DeleteReportQuery([FromBody] DeleteReportQueryRequest request)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            if (string.IsNullOrEmpty(userId))
            {
                return Unauthorized();
            }

            var entity = await _context.ReportQueries
                .FirstOrDefaultAsync(r => r.Id == request.Id && r.CreatedById == userId);

            if (entity == null)
            {
                return NotFound();
            }

            _context.ReportQueries.Remove(entity);
            await _context.SaveChangesAsync();

            return Json(new { success = true });
        }

        [Authorize(Policy = "reports.dynamic")]
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> ExecuteReportQuery([FromBody] ExecuteReportQueryRequest request)
        {
            var dataset = QueryBuilderDatasets.GetByKey(request.DatasetKey);
            if (dataset == null)
            {
                return BadRequest(new { message = "مجموعة البيانات غير معروفة." });
            }

            var queryable = dataset.QueryFactory(_context);

            if (!string.IsNullOrWhiteSpace(request.RulesJson))
            {
                if (!TryBuildPredicate(dataset, request.RulesJson, out var predicate, out var parameters, out var errorMessage))
                {
                    return BadRequest(new { message = errorMessage ?? "تعذر تحويل شروط التقرير." });
                }

                if (!string.IsNullOrEmpty(predicate))
                {
                    queryable = queryable.Where(predicate, parameters.ToArray());
                }
            }

            var selectedFields = GetSelectedFields(dataset, request.Columns);

            var limitedQuery = ApplyTake(queryable, 5000);
            var rows = await ToListAsyncDynamic(limitedQuery);

            var shapedRows = rows.Cast<object>().Select(row => ShapeRow(row, selectedFields)).ToList();

            var response = new
            {
                columns = selectedFields.Select(f => new { field = f.Field, label = f.Label }),
                rows = shapedRows
            };

            return Json(response);
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
                        .Include(r => r.PaymentAccount).ThenInclude(a => a.Branch)
                        .Include(r => r.Currency)
                        .Include(r => r.CreatedBy)
                        .Include(r => r.Supplier)
                        .Where(r => r.Date >= from && r.Date <= to)
                        .Select(r => new
                        {
                            r.Id,
                            r.Date,
                            Year = r.Date.Year,
                            Month = r.Date.Month,
                            Supplier = r.Supplier != null ? r.Supplier.NameAr : null,
                            SupplierAccountCode = r.Account.Code,
                            SupplierAccountName = r.Account.NameAr,
                            SupplierBranchCode = r.Account.Branch != null ? r.Account.Branch.Code : null,
                            SupplierBranchName = r.Account.Branch != null ? r.Account.Branch.NameAr : null,
                            PaymentAccountCode = r.PaymentAccount.Code,
                            PaymentAccountName = r.PaymentAccount.NameAr,
                            PaymentBranchCode = r.PaymentAccount.Branch != null ? r.PaymentAccount.Branch.Code : null,
                            PaymentBranchName = r.PaymentAccount.Branch != null ? r.PaymentAccount.Branch.NameAr : null,
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

        private static string GetFieldTypeString(QueryFieldType type)
        {
            return type switch
            {
                QueryFieldType.Number => "number",
                QueryFieldType.Decimal => "number",
                QueryFieldType.Date => "date",
                QueryFieldType.Boolean => "boolean",
                _ => "string"
            };
        }

        private static IReadOnlyList<QueryDatasetField> GetSelectedFields(QueryDatasetDefinition dataset, List<string>? requestedColumns)
        {
            if (requestedColumns == null || requestedColumns.Count == 0)
            {
                return dataset.Fields;
            }

            var selected = new List<QueryDatasetField>();
            foreach (var column in requestedColumns)
            {
                var field = dataset.Fields.FirstOrDefault(f => string.Equals(f.Field, column, StringComparison.OrdinalIgnoreCase));
                if (field != null)
                {
                    selected.Add(field);
                }
            }

            return selected.Count > 0 ? selected : dataset.Fields;
        }

        private static IQueryable ApplyTake(IQueryable source, int take)
        {
            var method = typeof(Queryable).GetMethods()
                .First(m => m.Name == nameof(Queryable.Take) && m.GetParameters().Length == 2);
            var generic = method.MakeGenericMethod(source.ElementType);
            return (IQueryable)generic.Invoke(null, new object[] { source, take })!;
        }

        private static IDictionary<string, object?> ShapeRow(object row, IReadOnlyList<QueryDatasetField> fields)
        {
            var result = new Dictionary<string, object?>();
            var type = row.GetType();

            foreach (var field in fields)
            {
                var property = type.GetProperty(field.Field, BindingFlags.Instance | BindingFlags.Public | BindingFlags.IgnoreCase);
                var value = property?.GetValue(row);
                result[field.Field] = value;
            }

            return result;
        }

        private static async Task<IList> ToListAsyncDynamic(IQueryable source)
        {
            var toListAsyncMethod = typeof(EntityFrameworkQueryableExtensions)
                .GetMethods(BindingFlags.Public | BindingFlags.Static)
                .First(m => m.Name == nameof(EntityFrameworkQueryableExtensions.ToListAsync) && m.GetParameters().Length == 2);

            var genericMethod = toListAsyncMethod.MakeGenericMethod(source.ElementType);
            var task = (Task)genericMethod.Invoke(null, new object[] { source, CancellationToken.None })!;

            await task.ConfigureAwait(false);

            var resultProperty = task.GetType().GetProperty("Result");
            if (resultProperty?.GetValue(task) is IList list)
            {
                return list;
            }

            return new List<object>();
        }

        private bool TryBuildPredicate(QueryDatasetDefinition dataset, string rulesJson, out string predicate, out List<object?> parameters, out string? errorMessage)
        {
            predicate = string.Empty;
            parameters = new List<object?>();
            errorMessage = null;

            QueryBuilderGroup? root;
            try
            {
                root = JsonSerializer.Deserialize<QueryBuilderGroup>(rulesJson, new JsonSerializerOptions
                {
                    PropertyNameCaseInsensitive = true,
                    NumberHandling = JsonNumberHandling.AllowReadingFromString | JsonNumberHandling.WriteAsString
                });
            }
            catch
            {
                errorMessage = "تعذر قراءة شروط التقرير.";
                return false;
            }

            if (root == null || root.Rules == null || root.Rules.Count == 0)
            {
                predicate = string.Empty;
                return true;
            }

            var parameterIndex = 0;
            var expression = BuildGroupExpression(root, dataset, parameters, ref parameterIndex, out errorMessage);

            if (errorMessage != null)
            {
                return false;
            }

            predicate = expression ?? string.Empty;
            return true;
        }

        private string? BuildGroupExpression(QueryBuilderGroup group, QueryDatasetDefinition dataset, List<object?> parameters, ref int parameterIndex, out string? errorMessage)
        {
            errorMessage = null;
            if (group.Rules == null || group.Rules.Count == 0)
            {
                return null;
            }

            var expressions = new List<string>();

            foreach (var rule in group.Rules)
            {
                string? expression = null;

                if (rule.Rules != null && rule.Rules.Count > 0)
                {
                    expression = BuildGroupExpression(rule.ToGroup(), dataset, parameters, ref parameterIndex, out errorMessage);
                }
                else if (!string.IsNullOrEmpty(rule.Field))
                {
                    var field = dataset.Fields.FirstOrDefault(f => string.Equals(f.Field, rule.Field, StringComparison.OrdinalIgnoreCase));
                    if (field == null)
                    {
                        errorMessage = $"الحقل {rule.Field} غير معروف.";
                        return null;
                    }

                    expression = BuildRuleExpression(field, rule, parameters, ref parameterIndex, out errorMessage);
                }

                if (errorMessage != null)
                {
                    return null;
                }

                if (!string.IsNullOrEmpty(expression))
                {
                    if (rule.Not)
                    {
                        expression = $"!({expression})";
                    }

                    expressions.Add(expression);
                }
            }

            if (expressions.Count == 0)
            {
                return null;
            }

            var separator = string.Equals(group.Condition, "or", StringComparison.OrdinalIgnoreCase) ? " or " : " and ";
            var combined = string.Join(separator, expressions.Select(e => $"({e})"));

            if (group.Not)
            {
                combined = $"!({combined})";
            }

            return combined;
        }

        private string? BuildRuleExpression(QueryDatasetField field, QueryBuilderRule rule, List<object?> parameters, ref int parameterIndex, out string? errorMessage)
        {
            errorMessage = null;
            var op = rule.Operator?.ToLowerInvariant();
            if (string.IsNullOrEmpty(op))
            {
                errorMessage = "نوع المعامل غير معروف.";
                return null;
            }

            switch (op)
            {
                case "equal":
                case "notequal":
                case "greaterthan":
                case "greaterthanorequal":
                case "lessthan":
                case "lessthanorequal":
                    {
                        var value = ConvertSingleValue(field, rule.Value, out errorMessage);
                        if (errorMessage != null)
                        {
                            return null;
                        }

                        parameters.Add(value);
                        var token = $"@{parameterIndex++}";

                        return op switch
                        {
                            "equal" => $"{field.Field} == {token}",
                            "notequal" => $"{field.Field} != {token}",
                            "greaterthan" => $"{field.Field} > {token}",
                            "greaterthanorequal" => $"{field.Field} >= {token}",
                            "lessthan" => $"{field.Field} < {token}",
                            "lessthanorequal" => $"{field.Field} <= {token}",
                            _ => null
                        };
                    }
                case "between":
                case "notbetween":
                    {
                        if (!TryConvertBetween(field, rule.Value, out var start, out var end, out errorMessage))
                        {
                            return null;
                        }

                        parameters.Add(start);
                        var startToken = $"@{parameterIndex++}";
                        parameters.Add(end);
                        var endToken = $"@{parameterIndex++}";

                        if (op == "between")
                        {
                            return $"({field.Field} >= {startToken} and {field.Field} <= {endToken})";
                        }

                        return $"({field.Field} < {startToken} or {field.Field} > {endToken})";
                    }
                case "contains":
                    {
                        var value = ConvertSingleValue(field, rule.Value, out errorMessage);
                        if (errorMessage != null)
                        {
                            return null;
                        }

                        parameters.Add(value);
                        var token = $"@{parameterIndex++}";
                        return $"{field.Field} != null && {field.Field}.Contains({token})";
                    }
                case "startswith":
                    {
                        var value = ConvertSingleValue(field, rule.Value, out errorMessage);
                        if (errorMessage != null)
                        {
                            return null;
                        }

                        parameters.Add(value);
                        var token = $"@{parameterIndex++}";
                        return $"{field.Field} != null && {field.Field}.StartsWith({token})";
                    }
                case "endswith":
                    {
                        var value = ConvertSingleValue(field, rule.Value, out errorMessage);
                        if (errorMessage != null)
                        {
                            return null;
                        }

                        parameters.Add(value);
                        var token = $"@{parameterIndex++}";
                        return $"{field.Field} != null && {field.Field}.EndsWith({token})";
                    }
                case "in":
                case "notin":
                    {
                        var values = ConvertMultipleValues(field, rule.Value, out errorMessage);
                        if (errorMessage != null)
                        {
                            return null;
                        }

                        parameters.Add(values);
                        var token = $"@{parameterIndex++}";
                        var clause = $"{token}.Contains({field.Field})";
                        return op == "notin" ? $"!({clause})" : clause;
                    }
                case "isnull":
                    return $"{field.Field} == null";
                case "isnotnull":
                    return $"{field.Field} != null";
                case "isempty":
                    return $"string.IsNullOrEmpty({field.Field})";
                case "isnotempty":
                    return $"!string.IsNullOrEmpty({field.Field})";
                default:
                    errorMessage = "نوع المعامل غير مدعوم.";
                    return null;
            }
        }

        private object? ConvertSingleValue(QueryDatasetField field, JsonElement valueElement, out string? errorMessage)
        {
            errorMessage = null;
            try
            {
                if (valueElement.ValueKind == JsonValueKind.Null || valueElement.ValueKind == JsonValueKind.Undefined)
                {
                    return null;
                }

                return field.FieldType switch
                {
                    QueryFieldType.Number => ConvertToInt(valueElement),
                    QueryFieldType.Decimal => ConvertToDecimal(valueElement),
                    QueryFieldType.Date => ConvertToDateTime(valueElement),
                    QueryFieldType.Boolean => ConvertToBoolean(valueElement),
                    _ => valueElement.GetString()
                };
            }
            catch
            {
                errorMessage = $"قيمة غير صالحة للحقل {field.Label}.";
                return null;
            }
        }

        private bool TryConvertBetween(QueryDatasetField field, JsonElement valueElement, out object? start, out object? end, out string? errorMessage)
        {
            errorMessage = null;
            start = null;
            end = null;

            if (valueElement.ValueKind != JsonValueKind.Array)
            {
                errorMessage = $"قيمة غير صالحة للحقل {field.Label}.";
                return false;
            }

            var array = valueElement.EnumerateArray().ToList();
            if (array.Count != 2)
            {
                errorMessage = $"قيمة غير صالحة للحقل {field.Label}.";
                return false;
            }

            start = ConvertSingleValue(field, array[0], out errorMessage);
            if (errorMessage != null)
            {
                return false;
            }

            end = ConvertSingleValue(field, array[1], out errorMessage);
            if (errorMessage != null)
            {
                return false;
            }

            return true;
        }

        private object ConvertMultipleValues(QueryDatasetField field, JsonElement valueElement, out string? errorMessage)
        {
            errorMessage = null;
            if (valueElement.ValueKind != JsonValueKind.Array)
            {
                errorMessage = $"قيمة غير صالحة للحقل {field.Label}.";
                return Array.Empty<object>();
            }

            try
            {
                return field.FieldType switch
                {
                    QueryFieldType.Number => valueElement.EnumerateArray().Select(ConvertToInt).ToList(),
                    QueryFieldType.Decimal => valueElement.EnumerateArray().Select(ConvertToDecimal).ToList(),
                    QueryFieldType.Date => valueElement.EnumerateArray().Select(ConvertToDateTime).ToList(),
                    QueryFieldType.Boolean => valueElement.EnumerateArray().Select(ConvertToBoolean).ToList(),
                    _ => valueElement.EnumerateArray().Select(e => e.GetString() ?? string.Empty).ToList()
                };
            }
            catch
            {
                errorMessage = $"قيمة غير صالحة للحقل {field.Label}.";
                return Array.Empty<object>();
            }
        }

        private static int ConvertToInt(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => element.TryGetInt32(out var intValue) ? intValue : (int)element.GetInt64(),
                JsonValueKind.String when int.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) => value,
                _ => throw new FormatException()
            };
        }

        private static decimal ConvertToDecimal(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.Number => element.GetDecimal(),
                JsonValueKind.String when decimal.TryParse(element.GetString(), NumberStyles.Any, CultureInfo.InvariantCulture, out var value) => value,
                _ => throw new FormatException()
            };
        }

        private static DateTime ConvertToDateTime(JsonElement element)
        {
            if (element.ValueKind == JsonValueKind.String)
            {
                var stringValue = element.GetString();
                if (DateTime.TryParse(stringValue, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var parsed))
                {
                    return parsed;
                }
            }
            else if (element.ValueKind == JsonValueKind.Number && element.TryGetInt64(out var unix))
            {
                return DateTimeOffset.FromUnixTimeMilliseconds(unix).DateTime;
            }

            throw new FormatException();
        }

        private static bool ConvertToBoolean(JsonElement element)
        {
            return element.ValueKind switch
            {
                JsonValueKind.True => true,
                JsonValueKind.False => false,
                JsonValueKind.String when bool.TryParse(element.GetString(), out var value) => value,
                _ => throw new FormatException()
            };
        }

        private class QueryBuilderGroup
        {
            public string? Condition { get; set; }
            public bool Not { get; set; }
            public List<QueryBuilderRule> Rules { get; set; } = new();
        }

        private class QueryBuilderRule
        {
            public string? Field { get; set; }
            public string? Operator { get; set; }
            public string? Type { get; set; }
            public JsonElement Value { get; set; }
            public bool Not { get; set; }
            public string? Condition { get; set; }
            public List<QueryBuilderRule>? Rules { get; set; }

            public QueryBuilderGroup ToGroup()
            {
                return new QueryBuilderGroup
                {
                    Condition = Condition,
                    Not = false,
                    Rules = Rules ?? new List<QueryBuilderRule>()
                };
            }
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
        public async Task<IActionResult> TrialBalance(int? branchId, DateTime? fromDate, DateTime? toDate, bool includePending = false, int? currencyId = null, int level = 5)
        {
            var accounts = await _context.Accounts
                .Include(a => a.Branch)
                .Include(a => a.Currency)
                .Where(a => a.CanPostTransactions)
                .Where(a => !branchId.HasValue || a.BranchId == branchId || a.BranchId == null)
                .OrderBy(a => a.Code)
                .ToListAsync();

            var fiscalYearStart = new DateTime(2025, 1, 1);
            var from = fromDate ?? fiscalYearStart;
            var to = toDate ?? DateTime.Today;

            var normalizedLevel = level;
            if (normalizedLevel < 1 || normalizedLevel > 5)
            {
                normalizedLevel = 5;
            }

            var filteredAccounts = accounts
                .Where(a => a.Level <= normalizedLevel)
                .ToList();

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
                Accounts = filteredAccounts.Select(a =>
                {
                    pending.TryGetValue(a.Id, out var p);
                    var pendingBalance = a.Nature == AccountNature.Debit ? p.Debit - p.Credit : p.Credit - p.Debit;
                    var balance = a.CurrentBalance + pendingBalance;
                    var balanceSelected = _currencyService.Convert(balance, a.Currency, selectedCurrency);
                    var balanceBase = _currencyService.Convert(balance, a.Currency, baseCurrency);

                    decimal debitSelected;
                    decimal creditSelected;
                    decimal debitBase;
                    decimal creditBase;

                    if (a.AccountType == AccountType.Liabilities)
                    {
                        if (balanceSelected < 0)
                        {
                            debitSelected = Math.Abs(balanceSelected);
                            creditSelected = 0;
                        }
                        else
                        {
                            debitSelected = 0;
                            creditSelected = balanceSelected;
                        }

                        if (balanceBase < 0)
                        {
                            debitBase = Math.Abs(balanceBase);
                            creditBase = 0;
                        }
                        else
                        {
                            debitBase = 0;
                            creditBase = balanceBase;
                        }
                    }
                    else
                    {
                        debitSelected = a.Nature == AccountNature.Debit ? balanceSelected : 0;
                        creditSelected = a.Nature == AccountNature.Credit ? balanceSelected : 0;
                        debitBase = a.Nature == AccountNature.Debit ? balanceBase : 0;
                        creditBase = a.Nature == AccountNature.Credit ? balanceBase : 0;
                    }

                    return new TrialBalanceAccountViewModel
                    {
                        AccountCode = a.Code,
                        AccountName = a.NameAr,
                        DebitBalance = debitSelected,
                        CreditBalance = creditSelected,
                        DebitBalanceBase = debitBase,
                        CreditBalanceBase = creditBase,
                        Level = a.Level
                    };
                }).ToList(),
                Branches = await GetBranchesSelectList(),
                Currencies = await _context.Currencies
                    .Select(c => new SelectListItem { Value = c.Id.ToString(), Text = c.Code })
                    .ToListAsync(),
                SelectedCurrencyId = selectedCurrency.Id,
                SelectedCurrencyCode = selectedCurrency.Code,
                BaseCurrencyCode = baseCurrency.Code,
                SelectedLevel = normalizedLevel,
                Levels = Enumerable.Range(1, 5)
                    .Select(l => new SelectListItem
                    {
                        Value = l.ToString(),
                        Text = l.ToString(),
                        Selected = l == normalizedLevel
                    })
                    .ToList()
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

        // GET: Reports/BranchExpenses
        public async Task<IActionResult> BranchExpenses(
            int[]? branchIds,
            DateTime? fromDate,
            DateTime? toDate,
            BranchExpensesViewMode viewMode = BranchExpensesViewMode.ByBranch,
            BranchExpensesPeriodGrouping periodGrouping = BranchExpensesPeriodGrouping.Monthly)
        {
            var defaultFrom = new DateTime(DateTime.Today.Year, 1, 1);
            var defaultTo = DateTime.Today;

            var parsedBranchIds = new List<int>();
            if (branchIds != null)
            {
                parsedBranchIds.AddRange(branchIds.Where(id => id > 0));
            }

            if (!parsedBranchIds.Any() && Request.Query.TryGetValue("branchIds", out var branchIdValues))
            {
                foreach (var value in branchIdValues.SelectMany(v => v.Split(',', StringSplitOptions.RemoveEmptyEntries)))
                {
                    if (int.TryParse(value, out var id) && id > 0)
                    {
                        parsedBranchIds.Add(id);
                    }
                }
            }

            DateTime? normalizedFromDate = fromDate;
            if (!normalizedFromDate.HasValue && Request.Query.TryGetValue("fromDate", out var fromDateValues))
            {
                foreach (var value in fromDateValues)
                {
                    if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    {
                        normalizedFromDate = parsed;
                        break;
                    }
                }
            }

            DateTime? normalizedToDate = toDate;
            if (!normalizedToDate.HasValue && Request.Query.TryGetValue("toDate", out var toDateValues))
            {
                foreach (var value in toDateValues)
                {
                    if (DateTime.TryParseExact(value, "yyyy-MM-dd", CultureInfo.InvariantCulture, DateTimeStyles.None, out var parsed))
                    {
                        normalizedToDate = parsed;
                        break;
                    }
                }
            }

            var model = new BranchExpensesReportViewModel
            {
                FromDate = (normalizedFromDate ?? defaultFrom).Date,
                ToDate = (normalizedToDate ?? defaultTo).Date,
                Branches = await GetBranchesSelectList(),
                SelectedBranchIds = parsedBranchIds.Distinct().ToList(),
                FiltersApplied = true,
                ViewMode = viewMode,
                PeriodGrouping = periodGrouping
            };

            if (model.FromDate > model.ToDate)
            {
                (model.FromDate, model.ToDate) = (model.ToDate, model.FromDate);
            }

            var culture = (CultureInfo)CultureInfo.CreateSpecificCulture("ar-SA").Clone();
            var gregorianCalendar = culture.OptionalCalendars
                .OfType<GregorianCalendar>()
                .FirstOrDefault();

            if (gregorianCalendar == null)
            {
                culture = (CultureInfo)CultureInfo.CreateSpecificCulture("ar-EG").Clone();
                gregorianCalendar = culture.OptionalCalendars
                    .OfType<GregorianCalendar>()
                    .FirstOrDefault() ?? new GregorianCalendar(GregorianCalendarTypes.USEnglish);
            }

            culture.DateTimeFormat.Calendar = gregorianCalendar;
            culture.DateTimeFormat.ShortDatePattern = "dd/MM/yyyy";
            culture.DateTimeFormat.LongDatePattern = "dd/MM/yyyy";
            culture.DateTimeFormat.MonthDayPattern = "dd/MM";
            model.DisplayCulture = culture;

            var columns = BuildBranchExpensesColumns(model.FromDate, model.ToDate, model.PeriodGrouping, culture);
            model.Columns = columns;

            if (!columns.Any())
            {
                return View(model);
            }

            var toExclusive = model.ToDate.AddDays(1);

            var expensesQuery = _context.Expenses
                .AsNoTracking()
                .Where(e => e.IsApproved)
                .Where(e => e.CreatedAt >= model.FromDate && e.CreatedAt < toExclusive);

            if (model.SelectedBranchIds.Any())
            {
                expensesQuery = expensesQuery.Where(e => model.SelectedBranchIds.Contains(e.BranchId));
            }

            var rawData = await expensesQuery
                .Select(e => new
                {
                    e.BranchId,
                    BranchName = e.Branch.NameAr,
                    e.CreatedAt,
                    e.Amount
                })
                .ToListAsync();

            var groupedData = rawData
                .Select(item => new
                {
                    item.BranchId,
                    item.BranchName,
                    PeriodStart = GetBranchExpensesPeriodStart(item.CreatedAt, model.PeriodGrouping),
                    item.Amount
                })
                .Where(x => x.PeriodStart >= columns.First().PeriodStart && x.PeriodStart <= columns.Last().PeriodStart)
                .GroupBy(x => new { x.BranchId, x.BranchName, x.PeriodStart })
                .Select(g => new
                {
                    g.Key.BranchId,
                    g.Key.BranchName,
                    g.Key.PeriodStart,
                    Total = g.Sum(x => x.Amount)
                })
                .ToList();

            var branchLookup = model.Branches
                .Where(b => int.TryParse(b.Value, out _))
                .ToDictionary(b => int.Parse(b.Value), b => b.Text);

            var columnTotals = columns.ToDictionary(c => c.PeriodStart, _ => 0m);
            var rows = new List<BranchExpensesReportRow>();
            decimal grandTotal = 0m;

            if (model.ViewMode == BranchExpensesViewMode.Combined)
            {
                var row = new BranchExpensesReportRow
                {
                    BranchId = 0,
                    BranchName = model.SelectedBranchIds.Any() ? "إجمالي الفروع المحددة" : "إجمالي جميع الفروع",
                    Amounts = columns.ToDictionary(c => c.PeriodStart, _ => 0m)
                };

                foreach (var column in columns)
                {
                    var periodTotal = groupedData
                        .Where(d => d.PeriodStart == column.PeriodStart)
                        .Sum(d => d.Total);

                    row.Amounts[column.PeriodStart] = periodTotal;
                    columnTotals[column.PeriodStart] += periodTotal;
                    grandTotal += periodTotal;
                }

                if (row.Total > 0 || groupedData.Any())
                {
                    rows.Add(row);
                }
            }
            else
            {
                var branchIdsToDisplay = model.SelectedBranchIds.Any()
                    ? model.SelectedBranchIds
                    : groupedData.Select(d => d.BranchId).Distinct().ToList();

                foreach (var branchId in branchIdsToDisplay)
                {
                    var row = new BranchExpensesReportRow
                    {
                        BranchId = branchId,
                        BranchName = branchLookup.TryGetValue(branchId, out var name) ? name : $"فرع #{branchId}",
                        Amounts = columns.ToDictionary(c => c.PeriodStart, _ => 0m)
                    };

                    foreach (var item in groupedData.Where(d => d.BranchId == branchId))
                    {
                        if (row.Amounts.ContainsKey(item.PeriodStart))
                        {
                            row.Amounts[item.PeriodStart] = item.Total;
                        }
                    }

                    if (!model.SelectedBranchIds.Any() && row.Total == 0)
                    {
                        continue;
                    }

                    rows.Add(row);

                    foreach (var column in columns)
                    {
                        columnTotals[column.PeriodStart] += row.Amounts[column.PeriodStart];
                    }

                    grandTotal += row.Total;
                }
            }

            model.Rows = rows.OrderBy(r => r.BranchName).ToList();
            model.ColumnTotals = columnTotals;
            model.GrandTotal = grandTotal;

            return View(model);
        }

        // GET: Reports/BranchIncomeStatement
        public async Task<IActionResult> BranchIncomeStatement(
            BranchIncomeStatementRangeMode rangeMode = BranchIncomeStatementRangeMode.DateRange,
            DateTime? fromDate = null,
            DateTime? toDate = null,
            int? year = null,
            int? quarter = null)
        {
            var today = DateTime.Today;
            var defaultFrom = new DateTime(today.Year, 1, 1);
            var defaultTo = today;

            var availableYearsRaw = await _context.JournalEntries
                .AsNoTracking()
                .Where(e => e.Status == JournalEntryStatus.Posted)
                .Select(e => e.Date.Year)
                .Distinct()
                .OrderByDescending(y => y)
                .ToListAsync();

            if (!availableYearsRaw.Any())
            {
                availableYearsRaw.Add(today.Year);
            }

            var selectedYear = year.HasValue && availableYearsRaw.Contains(year.Value)
                ? year.Value
                : availableYearsRaw.First();

            var currentQuarter = ((today.Month - 1) / 3) + 1;
            var selectedQuarter = quarter.HasValue && quarter.Value is >= 1 and <= 4
                ? quarter.Value
                : currentQuarter;
            selectedQuarter = Math.Max(1, Math.Min(4, selectedQuarter));

            DateTime actualFrom;
            DateTime actualTo;

            if (rangeMode == BranchIncomeStatementRangeMode.Quarter)
            {
                var quarterStartMonth = (selectedQuarter - 1) * 3 + 1;
                actualFrom = new DateTime(selectedYear, quarterStartMonth, 1);
                actualTo = actualFrom.AddMonths(3).AddDays(-1);
            }
            else
            {
                actualFrom = (fromDate ?? defaultFrom).Date;
                actualTo = (toDate ?? defaultTo).Date;
                if (actualFrom > actualTo)
                {
                    (actualFrom, actualTo) = (actualTo, actualFrom);
                }
            }

            var baseCurrency = await _context.Currencies.AsNoTracking().FirstAsync(c => c.IsBase);

            var lines = await _context.JournalEntryLines
                .AsNoTracking()
                .Include(l => l.JournalEntry)
                    .ThenInclude(j => j.Branch)
                .Include(l => l.Account)
                    .ThenInclude(a => a.Currency)
                .Where(l => l.Account.Classification == AccountClassification.IncomeStatement)
                .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted)
                .Where(l => l.JournalEntry.Date >= actualFrom && l.JournalEntry.Date <= actualTo)
                .ToListAsync();

            var rowsDictionary = new Dictionary<int?, BranchIncomeStatementRow>();

            foreach (var line in lines)
            {
                var branchId = line.JournalEntry.BranchId;
                var branchName = line.JournalEntry.Branch?.NameAr ?? "بدون فرع";

                if (!rowsDictionary.TryGetValue(branchId, out var row))
                {
                    row = new BranchIncomeStatementRow
                    {
                        BranchId = branchId,
                        BranchName = branchName
                    };
                    rowsDictionary[branchId] = row;
                }

                var amount = line.Account.Nature == AccountNature.Debit
                    ? line.DebitAmount - line.CreditAmount
                    : line.CreditAmount - line.DebitAmount;

                var accountCurrency = line.Account.Currency ?? baseCurrency;
                var amountInBase = _currencyService.Convert(amount, accountCurrency, baseCurrency);

                if (line.Account.AccountType == AccountType.Revenue)
                {
                    row.Revenue += amountInBase;
                }
                else if (line.Account.AccountType == AccountType.Expenses)
                {
                    row.Expenses += amountInBase;
                }
            }

            var rows = rowsDictionary.Values
                .Where(r => r.Revenue != 0 || r.Expenses != 0)
                .OrderBy(r => r.BranchName)
                .ToList();

            var model = new BranchIncomeStatementReportViewModel
            {
                RangeMode = rangeMode,
                FromDate = actualFrom,
                ToDate = actualTo,
                SelectedYear = selectedYear,
                SelectedQuarter = selectedQuarter,
                AvailableYears = availableYearsRaw
                    .OrderByDescending(y => y)
                    .Select(y => new SelectListItem
                    {
                        Value = y.ToString(),
                        Text = y.ToString(),
                        Selected = y == selectedYear
                    })
                    .ToList(),
                Rows = rows,
                TotalRevenue = rows.Sum(r => r.Revenue),
                TotalExpenses = rows.Sum(r => r.Expenses),
                BaseCurrencyCode = baseCurrency.Code,
                FiltersApplied = true
            };

            model.NetIncome = model.TotalRevenue - model.TotalExpenses;

            return View(model);
        }

        private async Task<BranchPerformanceSummaryViewModel> BuildBranchPerformanceSummaryViewModel(DateTime? fromDate, DateTime? toDate)
        {
            var today = DateTime.Today;
            var defaultFrom = new DateTime(today.Year, 1, 1);
            var actualFrom = (fromDate ?? defaultFrom).Date;
            var actualTo = (toDate ?? today).Date;

            if (actualFrom > actualTo)
            {
                (actualFrom, actualTo) = (actualTo, actualFrom);
            }

            var baseCurrency = await _context.Currencies.AsNoTracking().FirstAsync(c => c.IsBase);

            var lines = await _context.JournalEntryLines
                .AsNoTracking()
                .Include(l => l.JournalEntry)
                    .ThenInclude(j => j.Branch)
                .Include(l => l.Account)
                    .ThenInclude(a => a.Currency)
                .Include(l => l.Account)
                    .ThenInclude(a => a.Parent)
                .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted)
                .Where(l => l.JournalEntry.Date >= actualFrom && l.JournalEntry.Date <= actualTo)
                .ToListAsync();

            var branchNames = new Dictionary<int?, string?>();
            var sectionsRaw = new Dictionary<AccountType, Dictionary<string, BranchPerformanceSummaryRow>>();

            foreach (var line in lines)
            {
                var amount = line.Account.Nature == AccountNature.Debit
                    ? line.DebitAmount - line.CreditAmount
                    : line.CreditAmount - line.DebitAmount;

                if (amount == 0)
                {
                    continue;
                }

                var accountCurrency = line.Account.Currency ?? baseCurrency;
                var amountInBase = _currencyService.Convert(amount, accountCurrency, baseCurrency);

                if (amountInBase == 0)
                {
                    continue;
                }

                var branchId = line.JournalEntry.BranchId;
                var branchName = line.JournalEntry.Branch?.NameAr;

                if (!branchNames.ContainsKey(branchId))
                {
                    branchNames[branchId] = string.IsNullOrWhiteSpace(branchName) ? "بدون فرع" : branchName;
                }

                if (!sectionsRaw.TryGetValue(line.Account.AccountType, out var rowsDictionary))
                {
                    rowsDictionary = new Dictionary<string, BranchPerformanceSummaryRow>(StringComparer.OrdinalIgnoreCase);
                    sectionsRaw[line.Account.AccountType] = rowsDictionary;
                }

                var label = line.Account.Parent?.NameAr ?? line.Account.NameAr;
                var labelKey = string.IsNullOrWhiteSpace(label) ? line.Account.Code : label;

                if (!rowsDictionary.TryGetValue(labelKey, out var row))
                {
                    row = new BranchPerformanceSummaryRow
                    {
                        Label = labelKey
                    };
                    rowsDictionary[labelKey] = row;
                }

                if (row.Values.ContainsKey(branchId))
                {
                    row.Values[branchId] += amountInBase;
                }
                else
                {
                    row.Values[branchId] = amountInBase;
                }
            }

            var branches = branchNames
                .Select(kvp => new BranchPerformanceSummaryBranch
                {
                    BranchId = kvp.Key,
                    BranchName = kvp.Value ?? "بدون فرع"
                })
                .OrderBy(b => b.BranchName)
                .ToList();

            var sectionOrder = new[]
            {
                AccountType.Revenue,
                AccountType.Expenses,
                AccountType.Assets,
                AccountType.Liabilities,
                AccountType.Equity
            };

            var sections = new List<BranchPerformanceSummarySection>();

            foreach (var accountType in sectionOrder)
            {
                if (!sectionsRaw.TryGetValue(accountType, out var rowsDictionary))
                {
                    continue;
                }

                var rows = rowsDictionary.Values
                    .Where(r => r.Values.Values.Any(v => v != 0))
                    .OrderBy(r => r.Label)
                    .ToList();

                if (!rows.Any())
                {
                    continue;
                }

                var section = new BranchPerformanceSummarySection
                {
                    AccountType = accountType,
                    Title = accountType switch
                    {
                        AccountType.Assets => "الأصول",
                        AccountType.Liabilities => "الخصوم",
                        AccountType.Equity => "حقوق الملكية",
                        AccountType.Revenue => "الإيرادات",
                        AccountType.Expenses => "المصروفات",
                        _ => accountType.ToString()
                    },
                    Rows = rows
                };

                var totals = new Dictionary<int?, decimal>();
                foreach (var branch in branches)
                {
                    var total = rows.Sum(r => r.Values.TryGetValue(branch.BranchId, out var value) ? value : 0m);
                    if (total != 0)
                    {
                        totals[branch.BranchId] = total;
                    }
                }

                section.TotalsByBranch = totals;
                sections.Add(section);
            }

            var summaryRows = new List<BranchPerformanceSummaryRow>();

            if (branches.Any())
            {
                BranchPerformanceSummarySection? GetSection(AccountType type) =>
                    sections.FirstOrDefault(s => s.AccountType == type);

                var revenueSection = GetSection(AccountType.Revenue);
                var expensesSection = GetSection(AccountType.Expenses);
                var assetsSection = GetSection(AccountType.Assets);
                var liabilitiesSection = GetSection(AccountType.Liabilities);

                if (revenueSection != null || expensesSection != null)
                {
                    var netIncomeRow = new BranchPerformanceSummaryRow
                    {
                        Label = "صافي الدخل"
                    };

                    foreach (var branch in branches)
                    {
                        decimal revenueAmount = 0m;
                        if (revenueSection?.TotalsByBranch != null && revenueSection.TotalsByBranch.TryGetValue(branch.BranchId, out var revenueValue))
                        {
                            revenueAmount = revenueValue;
                        }

                        decimal expensesAmount = 0m;
                        if (expensesSection?.TotalsByBranch != null && expensesSection.TotalsByBranch.TryGetValue(branch.BranchId, out var expensesValue))
                        {
                            expensesAmount = expensesValue;
                        }

                        netIncomeRow.Values[branch.BranchId] = revenueAmount - expensesAmount;
                    }

                    summaryRows.Add(netIncomeRow);
                }

                if (assetsSection != null || liabilitiesSection != null)
                {
                    var netAssetsRow = new BranchPerformanceSummaryRow
                    {
                        Label = "صافي الأصول"
                    };

                    foreach (var branch in branches)
                    {
                        decimal assetsAmount = 0m;
                        if (assetsSection?.TotalsByBranch != null && assetsSection.TotalsByBranch.TryGetValue(branch.BranchId, out var assetsValue))
                        {
                            assetsAmount = assetsValue;
                        }

                        decimal liabilitiesAmount = 0m;
                        if (liabilitiesSection?.TotalsByBranch != null && liabilitiesSection.TotalsByBranch.TryGetValue(branch.BranchId, out var liabilitiesValue))
                        {
                            liabilitiesAmount = liabilitiesValue;
                        }

                        netAssetsRow.Values[branch.BranchId] = assetsAmount - liabilitiesAmount;
                    }

                    summaryRows.Add(netAssetsRow);
                }
            }

            return new BranchPerformanceSummaryViewModel
            {
                FromDate = actualFrom,
                ToDate = actualTo,
                BaseCurrencyCode = baseCurrency.Code,
                Branches = branches,
                Sections = sections,
                SummaryRows = summaryRows,
                FiltersApplied = true
            };
        }

        private static List<BranchExpensesReportColumn> BuildBranchExpensesColumns(DateTime fromDate, DateTime toDate, BranchExpensesPeriodGrouping grouping, CultureInfo culture)
        {
            var columns = new List<BranchExpensesReportColumn>();

            if (fromDate > toDate)
            {
                (fromDate, toDate) = (toDate, fromDate);
            }

            var start = GetBranchExpensesPeriodStart(fromDate, grouping);
            var end = GetBranchExpensesPeriodStart(toDate, grouping);

            var cursor = start;
            while (cursor <= end)
            {
                var column = new BranchExpensesReportColumn
                {
                    PeriodStart = cursor,
                    PeriodEnd = GetBranchExpensesNextPeriodStart(cursor, grouping).AddDays(-1),
                    Label = GetBranchExpensesPeriodLabel(cursor, grouping, culture)
                };

                columns.Add(column);
                cursor = GetBranchExpensesNextPeriodStart(cursor, grouping);
            }

            return columns;
        }

        private static DateTime GetBranchExpensesPeriodStart(DateTime date, BranchExpensesPeriodGrouping grouping)
        {
            return grouping switch
            {
                BranchExpensesPeriodGrouping.Monthly => new DateTime(date.Year, date.Month, 1),
                BranchExpensesPeriodGrouping.Quarterly =>
                    new DateTime(date.Year, ((date.Month - 1) / 3) * 3 + 1, 1),
                BranchExpensesPeriodGrouping.Yearly => new DateTime(date.Year, 1, 1),
                _ => new DateTime(date.Year, date.Month, 1)
            };
        }

        private static DateTime GetBranchExpensesNextPeriodStart(DateTime periodStart, BranchExpensesPeriodGrouping grouping)
        {
            return grouping switch
            {
                BranchExpensesPeriodGrouping.Monthly => periodStart.AddMonths(1),
                BranchExpensesPeriodGrouping.Quarterly => periodStart.AddMonths(3),
                BranchExpensesPeriodGrouping.Yearly => periodStart.AddYears(1),
                _ => periodStart.AddMonths(1)
            };
        }

        private static string GetBranchExpensesPeriodLabel(DateTime periodStart, BranchExpensesPeriodGrouping grouping, CultureInfo culture)
        {
            return grouping switch
            {
                BranchExpensesPeriodGrouping.Monthly => periodStart.ToString("MM/yyyy", culture),
                BranchExpensesPeriodGrouping.Quarterly => $"الربع {GetQuarterNumber(periodStart)} {periodStart.Year}",
                BranchExpensesPeriodGrouping.Yearly => periodStart.Year.ToString(),
                _ => periodStart.ToString("yyyy-MM")
            };
        }

        private static int GetQuarterNumber(DateTime periodStart)
        {
            return ((periodStart.Month - 1) / 3) + 1;
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
                    page.Header().Text($"الميزانية العمومية - {model.AsOfDate:dd/MM/yyyy}").FontSize(16).Bold();
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
                    page.Header().Text($"قائمة الدخل - {model.FromDate:dd/MM/yyyy} إلى {model.ToDate:dd/MM/yyyy}").FontSize(16).Bold();
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
            var viewModel = await BuildAccountStatementViewModel(accountId, branchId, fromDate, toDate);
            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> PrintAccountStatement(int? accountId, int? branchId, DateTime? fromDate, DateTime? toDate)
        {
            var viewModel = await BuildAccountStatementViewModel(accountId, branchId, fromDate, toDate);
            if (viewModel.AccountId == null)
            {
                return RedirectToAction(nameof(AccountStatement), new { accountId, branchId, fromDate, toDate });
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

        private async Task<AccountStatementViewModel> BuildAccountStatementViewModel(int? accountId, int? branchId, DateTime? fromDate, DateTime? toDate)
        {
            var baseCurrency = await _context.Currencies.FirstAsync(c => c.IsBase);
            var accounts = await _context.Accounts
                .Where(a => a.CanPostTransactions)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Code} - {a.NameAr}",
                    Selected = accountId.HasValue && a.Id == accountId.Value
                }).ToListAsync();

            var branches = await GetBranchesSelectList();
            foreach (var branch in branches)
            {
                if (branchId.HasValue && int.TryParse(branch.Value, out var branchValue))
                {
                    branch.Selected = branchValue == branchId.Value;
                }
            }

            var normalizedFromDate = (fromDate ?? DateTime.Now.AddMonths(-1)).Date;
            var normalizedToDate = (toDate ?? DateTime.Now).Date.AddDays(1).AddTicks(-1);

            var viewModel = new AccountStatementViewModel
            {
                FromDate = normalizedFromDate,
                ToDate = normalizedToDate,
                BranchId = branchId,
                Accounts = accounts,
                Branches = branches,
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

                    var priorLinesQuery = _context.JournalEntryLines
                        .AsNoTracking()
                        .Where(l => l.AccountId == accountId.Value)
                        .Where(l => !branchId.HasValue || l.JournalEntry.BranchId == branchId)
                        .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted)
                        .Where(l => l.JournalEntry.Date < viewModel.FromDate);

                    decimal running = account.OpeningBalance;
                    decimal runningBase = _currencyService.Convert(running, account.Currency, baseCurrency);

                    var priorDebitTotal = await priorLinesQuery.SumAsync(line => line.DebitAmount);
                    var priorCreditTotal = await priorLinesQuery.SumAsync(line => line.CreditAmount);
                    var priorNet = account.Nature == AccountNature.Debit
                        ? priorDebitTotal - priorCreditTotal
                        : priorCreditTotal - priorDebitTotal;
                    var priorNetBase = _currencyService.Convert(priorNet, account.Currency, baseCurrency);

                    running += priorNet;
                    runningBase += priorNetBase;

                    var openingBalance = running;
                    var openingBalanceBase = runningBase;

                    var lines = await _context.JournalEntryLines
                        .AsNoTracking()
                        .Include(l => l.JournalEntry)
                        .Where(l => l.AccountId == accountId.Value)
                        .Where(l => !branchId.HasValue || l.JournalEntry.BranchId == branchId)
                        .Where(l => l.JournalEntry.Status == JournalEntryStatus.Posted)
                        .Where(l => l.JournalEntry.Date >= viewModel.FromDate && l.JournalEntry.Date <= viewModel.ToDate)
                        .OrderBy(l => l.JournalEntry.Date)
                        .ThenBy(l => l.JournalEntry.Number)
                        .ToListAsync();

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
                            JournalEntryId = line.JournalEntryId,
                            JournalEntryNumber = line.JournalEntry.Number,
                            Reference = line.JournalEntry.Reference ?? string.Empty,
                            MovementType = line.JournalEntry.Description,
                            Description = line.Description ?? string.Empty,
                            DebitAmount = line.DebitAmount,
                            CreditAmount = line.CreditAmount,
                            RunningBalance = running,
                            DebitAmountBase = debitBase,
                            CreditAmountBase = creditBase,
                            RunningBalanceBase = runningBase
                        });
                    }

                    viewModel.OpeningBalance = openingBalance;
                    viewModel.OpeningBalanceBase = openingBalanceBase;
                    viewModel.ClosingBalance = running;
                    viewModel.ClosingBalanceBase = runningBase;
                    viewModel.TotalDebit = viewModel.Transactions.Sum(t => t.DebitAmount);
                    viewModel.TotalCredit = viewModel.Transactions.Sum(t => t.CreditAmount);
                    viewModel.TotalDebitBase = viewModel.Transactions.Sum(t => t.DebitAmountBase);
                    viewModel.TotalCreditBase = viewModel.Transactions.Sum(t => t.CreditAmountBase);
                }
            }

            return viewModel;
        }
    }
}
