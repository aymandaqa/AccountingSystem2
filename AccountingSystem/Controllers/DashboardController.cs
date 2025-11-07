using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.ViewModels;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "dashboard.view")]
    public class DashboardController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly RoadFnDbContext _roadContext;
        private readonly ILogger<DashboardController> _logger;
        private readonly ICurrencyService _currencyService;

        public DashboardController(ApplicationDbContext context, RoadFnDbContext roadContext, ILogger<DashboardController> logger, ICurrencyService currencyService)
        {
            _context = context;
            _roadContext = roadContext;
            _logger = logger;
            _currencyService = currencyService;
        }
        [Authorize]
        public async Task<IActionResult> Index(int? branchId = null, DateTime? fromDate = null, DateTime? toDate = null, int? currencyId = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == userId)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            if (branchId.HasValue && !userBranchIds.Contains(branchId.Value))
            {
                branchId = null;
            }

            var treeData = await ComputeDashboardTreeAsync(userBranchIds, branchId, fromDate, toDate, currencyId);

            var driverCodBranchIds = branchId.HasValue
                ? new List<int> { branchId.Value }
                : new List<int>(userBranchIds);

            var driverCodSummaries = await GetDriverCodBranchSummariesAsync(driverCodBranchIds);

            var accountTypeTrees = treeData.RootNodes
                .GroupBy(n => n.AccountType)
                .Select(g => new AccountTreeNodeViewModel
                {
                    Id = 0,
                    NameAr = g.Key.ToString(),
                    ParentAccountName = string.Empty,
                    AccountType = g.Key,
                    Level = 0,
                    Balance = g.Sum(n => n.Balance),
                    BalanceSelected = g.Sum(n => n.BalanceSelected),
                    BalanceBase = g.Sum(n => n.BalanceBase),
                    HasChildren = g.Any(),
                    Children = new List<AccountTreeNodeViewModel>()
                })
                .ToList();

            decimal GetTotal(AccountType type, bool isBase)
            {
                return treeData.TotalsByType.TryGetValue(type, out var totals)
                    ? (isBase ? totals.Base : totals.Selected)
                    : 0m;
            }

            var viewModel = new DashboardViewModel
            {
                SelectedBranchId = branchId,
                SelectedCurrencyId = treeData.SelectedCurrency.Id,
                SelectedCurrencyCode = treeData.SelectedCurrency.Code,
                BaseCurrencyCode = treeData.BaseCurrency.Code,
                FromDate = treeData.StartDate,
                ToDate = treeData.EndDate,
                TotalAssets = GetTotal(AccountType.Assets, false),
                TotalLiabilities = GetTotal(AccountType.Liabilities, false),
                TotalEquity = GetTotal(AccountType.Equity, false),
                TotalRevenues = GetTotal(AccountType.Revenue, false),
                TotalExpenses = GetTotal(AccountType.Expenses, false),
                TotalAssetsBase = GetTotal(AccountType.Assets, true),
                TotalLiabilitiesBase = GetTotal(AccountType.Liabilities, true),
                TotalEquityBase = GetTotal(AccountType.Equity, true),
                TotalRevenuesBase = GetTotal(AccountType.Revenue, true),
                TotalExpensesBase = GetTotal(AccountType.Expenses, true),
                AccountTypeTrees = accountTypeTrees,
                CashBoxTree = treeData.CashBoxTree,
                CashBoxParentAccountConfigured = treeData.CashBoxParentAccountConfigured,
                ParentAccountTree = treeData.ParentAccountTree,
                ParentAccountConfigured = treeData.ParentAccountConfigured,
                SelectedParentAccountName = treeData.SelectedParentAccountName,
                DriverCodBranchSummaries = driverCodSummaries
            };

            viewModel.NetIncome = viewModel.TotalRevenues - viewModel.TotalExpenses;
            viewModel.NetIncomeBase = viewModel.TotalRevenuesBase - viewModel.TotalExpensesBase;

            ViewBag.Branches = await _context.Branches
                .Where(b => b.IsActive && userBranchIds.Contains(b.Id))
                .Select(b => new { b.Id, b.NameAr })
                .ToListAsync();

            ViewBag.Currencies = await _context.Currencies
                .Select(c => new { c.Id, c.Code })
                .ToListAsync();

            return View(viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> LoadAccountTreeNodes(AccountType? accountType, int? parentId = null, int? branchId = null, DateTime? fromDate = null, DateTime? toDate = null, int? currencyId = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == userId)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            if (branchId.HasValue && !userBranchIds.Contains(branchId.Value))
            {
                branchId = null;
            }

            var treeData = await ComputeDashboardTreeAsync(userBranchIds, branchId, fromDate, toDate, currencyId);

            IEnumerable<AccountTreeNodeViewModel> nodesToRender;

            if (parentId.HasValue && parentId.Value > 0)
            {
                if (treeData.NodesById.TryGetValue(parentId.Value, out var parentNode))
                {
                    nodesToRender = parentNode.Children.OrderBy(n => n.Code);
                }
                else
                {
                    nodesToRender = Enumerable.Empty<AccountTreeNodeViewModel>();
                }
            }
            else
            {
                if (!accountType.HasValue)
                {
                    nodesToRender = Enumerable.Empty<AccountTreeNodeViewModel>();
                }
                else
                {
                    nodesToRender = treeData.RootNodes
                        .Where(n => n.AccountType == accountType.Value)
                        .OrderBy(n => n.Code);
                }
            }

            var sanitizedNodes = nodesToRender
                .Select(n => new AccountTreeNodeViewModel
                {
                    Id = n.Id,
                    Code = n.Code,
                    NameAr = n.NameAr,
                    ParentAccountName = n.ParentAccountName,
                    AccountType = n.AccountType,
                    Nature = n.Nature,
                    CurrencyCode = n.CurrencyCode,
                    OpeningBalance = n.OpeningBalance,
                    Balance = n.Balance,
                    CurrentBalance = n.CurrentBalance,
                    CurrentBalanceSelected = n.CurrentBalanceSelected,
                    CurrentBalanceBase = n.CurrentBalanceBase,
                    BalanceSelected = n.BalanceSelected,
                    BalanceBase = n.BalanceBase,
                    IsActive = n.IsActive,
                    CanPostTransactions = n.CanPostTransactions,
                    ParentId = n.ParentId,
                    Level = n.Level,
                    HasChildren = n.Children.Any(),
                    Children = new List<AccountTreeNodeViewModel>()
                })
                .ToList();

            ViewData["SelectedCurrencyCode"] = treeData.SelectedCurrency.Code;
            ViewData["BaseCurrencyCode"] = treeData.BaseCurrency.Code;
            ViewData["ShowActualBalances"] = true;

            return PartialView("~/Views/Shared/_AccountBalanceTreeNode.cshtml", sanitizedNodes);
        }

        [HttpGet]
        public async Task<IActionResult> LoadCashBoxTree(int? branchId = null, DateTime? fromDate = null, DateTime? toDate = null, int? currencyId = null)
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == userId)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            if (branchId.HasValue && !userBranchIds.Contains(branchId.Value))
            {
                branchId = null;
            }

            var treeData = await ComputeDashboardTreeAsync(userBranchIds, branchId, fromDate, toDate, currencyId);

            List<AccountTreeNodeViewModel> CloneNodes(IEnumerable<AccountTreeNodeViewModel> source)
            {
                return source
                    .OrderBy(n => n.Code)
                    .Select(n => new AccountTreeNodeViewModel
                    {
                        Id = n.Id,
                        Code = n.Code,
                        Name = n.Name,
                        NameAr = n.NameAr,
                        ParentAccountName = n.ParentAccountName,
                        AccountType = n.AccountType,
                        Nature = n.Nature,
                        CurrencyCode = n.CurrencyCode,
                        OpeningBalance = n.OpeningBalance,
                        CurrentBalance = n.CurrentBalance,
                        CurrentBalanceSelected = n.CurrentBalanceSelected,
                        CurrentBalanceBase = n.CurrentBalanceBase,
                        Balance = n.Balance,
                        BalanceSelected = n.BalanceSelected,
                        BalanceBase = n.BalanceBase,
                        IsActive = n.IsActive,
                        CanPostTransactions = n.CanPostTransactions,
                        ParentId = n.ParentId,
                        Level = n.Level,
                        HasChildren = n.Children.Any(),
                        Children = CloneNodes(n.Children)
                    })
                    .ToList();
            }

            var sanitizedNodes = CloneNodes(treeData.CashBoxTree);

            ViewData["SelectedCurrencyCode"] = treeData.SelectedCurrency.Code;
            ViewData["BaseCurrencyCode"] = treeData.BaseCurrency.Code;
            ViewData["ShowActualBalances"] = true;

            var viewModel = new CashBoxTreeViewModel
            {
                Nodes = sanitizedNodes,
                ParentConfigured = treeData.CashBoxParentAccountConfigured
            };

            return PartialView("~/Views/Dashboard/_CashBoxTreeContent.cshtml", viewModel);
        }

        [HttpGet]
        public async Task<IActionResult> GetBranches()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var branches = await _context.UserBranches
                .Where(ub => ub.UserId == userId && ub.Branch.IsActive)
                .Select(ub => new { id = ub.BranchId, nameAr = ub.Branch.NameAr })
                .ToListAsync();

            return Json(branches);
        }

        [HttpGet]
        public async Task<IActionResult> LoadDriverCodBranchDetails(int branchId)
        {
            if (branchId <= 0)
            {
                return PartialView("~/Views/Dashboard/_DriverCodBranchDetails.cshtml", new List<DriverCodBranchDetailViewModel>());
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == userId)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            if (!userBranchIds.Any())
            {
                return PartialView("~/Views/Dashboard/_DriverCodBranchDetails.cshtml", new List<DriverCodBranchDetailViewModel>());
            }

            if (!userBranchIds.Contains(branchId))
            {
                return PartialView("~/Views/Dashboard/_DriverCodBranchDetails.cshtml", new List<DriverCodBranchDetailViewModel>());
            }

            var details = await GetDriverCodBranchDetailsAsync(branchId);

            return PartialView("~/Views/Dashboard/_DriverCodBranchDetails.cshtml", details);
        }

        private async Task<List<DriverCodBranchSummaryViewModel>> GetDriverCodBranchSummariesAsync(List<int> allowedBranchIds)
        {
            var results = new List<DriverCodBranchSummaryViewModel>();
            var restrictBranches = allowedBranchIds?.Any() == true;

            const string sql = @"select sum(p.ShipmentTotal) as ShipmentTotal,
                                        sum(p.ShipmentCod) as ShipmentCod,
                                        b.NameAr as Branch,
                                        b.Id as BranchId
                                 from  CARGOTest.dbo.DriverPayCOD p
                                 left join AccountingSystemDbNew.dbo.Branches b on b.Code = p.ActiveBranchID
                                 group by b.NameAr, b.Id";

            var connection = _roadContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            try
            {
                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandType = CommandType.Text;

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var branchIdValue = reader.IsDBNull(3) ? (int?)null : reader.GetInt32(3);

                    if (!branchIdValue.HasValue)
                    {
                        continue;
                    }

                    if (restrictBranches && !allowedBranchIds.Contains(branchIdValue.Value))
                    {
                        continue;
                    }

                    var branchName = reader.IsDBNull(2) ? "فرع غير معروف" : reader.GetString(2);

                    var summary = new DriverCodBranchSummaryViewModel
                    {
                        BranchId = branchIdValue.Value,
                        BranchName = branchName,
                        ShipmentTotal = reader.IsDBNull(0) ? 0m : Convert.ToDecimal(reader.GetValue(0)),
                        ShipmentCod = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1))
                    };

                    results.Add(summary);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load Driver COD branch summaries");
            }
            finally
            {
                if (shouldClose && connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }

            return results
                .OrderByDescending(r => r.ShipmentCod)
                .ThenBy(r => r.BranchName)
                .ToList();
        }

        private async Task<List<DriverCodBranchDetailViewModel>> GetDriverCodBranchDetailsAsync(int branchId)
        {
            var results = new List<DriverCodBranchDetailViewModel>();

            const string sql = @"select   p.ShipmentTotal,
                                            p.ShipmentCod,
                                            b.NameAr   as Branch,
                                            b.Id as BranchId,
                                            p.DriverName,
                                            p.DriverID
                                     from   CARGOTest.dbo.DriverPayCOD p
                                     left join AccountingSystemDbNew.dbo.Branches b on b.Code = p.ActiveBranchID
                                     where b.Id = @BranchId";

            var connection = _roadContext.Database.GetDbConnection();
            var shouldClose = connection.State != ConnectionState.Open;

            try
            {
                if (shouldClose)
                {
                    await connection.OpenAsync();
                }

                using var command = connection.CreateCommand();
                command.CommandText = sql;
                command.CommandType = CommandType.Text;

                var parameter = command.CreateParameter();
                parameter.ParameterName = "@BranchId";
                parameter.Value = branchId;
                parameter.DbType = DbType.Int32;
                command.Parameters.Add(parameter);

                using var reader = await command.ExecuteReaderAsync();
                while (await reader.ReadAsync())
                {
                    var detail = new DriverCodBranchDetailViewModel
                    {
                        BranchId = branchId,
                        BranchName = reader.IsDBNull(2) ? string.Empty : reader.GetString(2),
                        DriverName = reader.IsDBNull(4) ? string.Empty : reader.GetString(4),
                        DriverId = reader.IsDBNull(5) ? string.Empty : reader.GetValue(5)?.ToString() ?? string.Empty,
                        ShipmentTotal = reader.IsDBNull(0) ? 0m : Convert.ToDecimal(reader.GetValue(0)),
                        ShipmentCod = reader.IsDBNull(1) ? 0m : Convert.ToDecimal(reader.GetValue(1))
                    };

                    results.Add(detail);
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load Driver COD branch details for branch {BranchId}", branchId);
            }
            finally
            {
                if (shouldClose && connection.State == ConnectionState.Open)
                {
                    await connection.CloseAsync();
                }
            }

            return results
                .OrderByDescending(r => r.ShipmentCod)
                .ThenBy(r => r.DriverName)
                .ToList();
        }

        private async Task<DashboardTreeComputationResult> ComputeDashboardTreeAsync(List<int> userBranchIds, int? branchId, DateTime? fromDate, DateTime? toDate, int? currencyId)
        {
            var effectiveBranchId = branchId.HasValue && userBranchIds.Contains(branchId.Value)
                ? branchId
                : null;

            var allowedBranchIds = effectiveBranchId.HasValue
                ? new List<int> { effectiveBranchId.Value }
                : new List<int>(userBranchIds);

            var cashBoxParentSetting = await _context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "CashBoxesParentAccountId");

            int? cashBoxParentAccountId = null;
            if (cashBoxParentSetting != null && int.TryParse(cashBoxParentSetting.Value, out var parsedParentId))
            {
                cashBoxParentAccountId = parsedParentId;
            }

            var cashBoxParentConfigured = cashBoxParentAccountId.HasValue;

            var dashboardParentSetting = await _context.SystemSettings
                .AsNoTracking()
                .FirstOrDefaultAsync(s => s.Key == "DashboardParentAccountId");

            int? dashboardParentAccountId = null;
            if (dashboardParentSetting != null && int.TryParse(dashboardParentSetting.Value, out var parsedDashboardParentId))
            {
                dashboardParentAccountId = parsedDashboardParentId;
            }

            var startDate = fromDate?.Date ?? DateTime.Now.AddDays(-30);
            var toDateValue = toDate?.Date ?? DateTime.Today;
            var endDate = toDateValue.Date.AddDays(1).AddTicks(-1);

            var accounts = await _context.Accounts
                .Where(a => !allowedBranchIds.Any() || a.BranchId == null || allowedBranchIds.Contains(a.BranchId.Value))
                .Include(a => a.Branch)
                .Include(a => a.JournalEntryLines)
                    .ThenInclude(l => l.JournalEntry)
                .Include(a => a.Currency)
                .Include(a => a.Parent)
                .AsNoTracking()
                .ToListAsync();

            var baseCurrency = await _context.Currencies.FirstAsync(c => c.IsBase);
            var selectedCurrency = currencyId.HasValue
                ? await _context.Currencies.FirstOrDefaultAsync(c => c.Id == currencyId.Value)
                : baseCurrency;
            selectedCurrency ??= baseCurrency;

            var hasBranchFilter = allowedBranchIds.Any();

            var accountBalances = accounts.ToDictionary(a => a.Id, a =>
            {
                var journalLines = a.JournalEntryLines
                    .Where(l => l.JournalEntry.Date >= startDate && l.JournalEntry.Date <= endDate);

                if (hasBranchFilter)
                {
                    journalLines = journalLines
                        .Where(l => allowedBranchIds.Contains(l.JournalEntry.BranchId));
                }

                return a.OpeningBalance + journalLines.Sum(l => l.DebitAmount - l.CreditAmount);
            });

            var nodes = accounts.Select(a =>
            {
                var balance = accountBalances[a.Id];
                return new AccountTreeNodeViewModel
                {
                    Id = a.Id,
                    Code = a.Code,
                    Name = a.NameEn ?? a.NameAr,
                    NameAr = a.NameAr,
                    ParentAccountName = a.Parent != null ? a.Parent.NameAr : string.Empty,
                    AccountType = a.AccountType,
                    Nature = a.Nature,
                    CurrencyCode = a.Currency.Code,
                    OpeningBalance = a.OpeningBalance,
                    CurrentBalance = a.CurrentBalance,
                    CurrentBalanceSelected = _currencyService.Convert(a.CurrentBalance, a.Currency, selectedCurrency),
                    CurrentBalanceBase = _currencyService.Convert(a.CurrentBalance, a.Currency, baseCurrency),
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

            void SortChildren(AccountTreeNodeViewModel node)
            {
                node.Children = node.Children
                    .OrderBy(c => c.Code ?? string.Empty)
                    .ToList();

                node.HasChildren = node.Children.Any();

                foreach (var child in node.Children)
                {
                    SortChildren(child);
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
                    node.CurrentBalance = node.Children.Sum(c => c.CurrentBalance);
                    node.CurrentBalanceSelected = node.Children.Sum(c => c.CurrentBalanceSelected);
                    node.CurrentBalanceBase = node.Children.Sum(c => c.CurrentBalanceBase);
                }
            }

            var rootNodes = nodes.Values
                .Where(n => n.ParentId == null)
                .OrderBy(n => n.Code ?? string.Empty)
                .ToList();

            foreach (var root in rootNodes)
            {
                SortChildren(root);
                ComputeBalances(root);
            }

            var totals = rootNodes
                .GroupBy(n => n.AccountType)
                .ToDictionary(g => g.Key, g => (
                    Selected: g.Sum(n => n.BalanceSelected),
                    Base: g.Sum(n => n.BalanceBase)));

            List<AccountTreeNodeViewModel> cashBoxTreeNodes = new();

            if (cashBoxParentAccountId.HasValue && nodes.TryGetValue(cashBoxParentAccountId.Value, out var cashBoxParentNode))
            {
                AccountTreeNodeViewModel CloneNode(AccountTreeNodeViewModel source, int relativeLevel)
                {
                    var clone = new AccountTreeNodeViewModel
                    {
                        Id = source.Id,
                        Code = source.Code,
                        Name = source.Name,
                        NameAr = source.NameAr,
                        ParentAccountName = source.ParentAccountName,
                        AccountType = source.AccountType,
                        Nature = source.Nature,
                        CurrencyCode = source.CurrencyCode,
                        OpeningBalance = source.OpeningBalance,
                        CurrentBalance = source.CurrentBalance,
                        CurrentBalanceSelected = source.CurrentBalanceSelected,
                        CurrentBalanceBase = source.CurrentBalanceBase,
                        Balance = source.Balance,
                        BalanceSelected = source.BalanceSelected,
                        BalanceBase = source.BalanceBase,
                        IsActive = source.IsActive,
                        CanPostTransactions = source.CanPostTransactions,
                        ParentId = relativeLevel == 0 ? null : source.ParentId,
                        Level = relativeLevel,
                        Children = new List<AccountTreeNodeViewModel>(),
                        HasChildren = source.Children.Any()
                    };

                    foreach (var child in source.Children.OrderBy(c => c.Code))
                    {
                        var childClone = CloneNode(child, relativeLevel + 1);
                        clone.Children.Add(childClone);
                    }

                    clone.HasChildren = clone.Children.Any();
                    return clone;
                }

                cashBoxTreeNodes.Add(CloneNode(cashBoxParentNode, 0));
            }

            AccountTreeNodeViewModel CloneParentTreeNode(AccountTreeNodeViewModel source, int relativeLevel)
            {
                var clone = new AccountTreeNodeViewModel
                {
                    Id = source.Id,
                    Code = source.Code,
                    Name = source.Name,
                    NameAr = source.NameAr,
                    ParentAccountName = source.ParentAccountName,
                    AccountType = source.AccountType,
                    Nature = source.Nature,
                    CurrencyCode = source.CurrencyCode,
                    OpeningBalance = source.OpeningBalance,
                    CurrentBalance = source.CurrentBalance,
                    CurrentBalanceSelected = source.CurrentBalanceSelected,
                    CurrentBalanceBase = source.CurrentBalanceBase,
                    Balance = source.Balance,
                    BalanceSelected = source.BalanceSelected,
                    BalanceBase = source.BalanceBase,
                    IsActive = source.IsActive,
                    CanPostTransactions = source.CanPostTransactions,
                    ParentId = relativeLevel == 0 ? null : source.ParentId,
                    Level = relativeLevel,
                    Children = new List<AccountTreeNodeViewModel>()
                };

                foreach (var child in source.Children)
                {
                    var childClone = CloneParentTreeNode(child, relativeLevel + 1);
                    clone.Children.Add(childClone);
                }

                clone.HasChildren = clone.Children.Any();
                return clone;
            }

            var parentAccountTreeNodes = new List<AccountTreeNodeViewModel>();
            var parentAccountConfigured = false;
            string? selectedParentAccountName = null;

            if (dashboardParentAccountId.HasValue && nodes.TryGetValue(dashboardParentAccountId.Value, out var parentAccountNode))
            {
                parentAccountConfigured = true;
                var parentName = parentAccountNode.NameAr ?? parentAccountNode.Name ?? string.Empty;
                selectedParentAccountName = string.IsNullOrWhiteSpace(parentAccountNode.Code)
                    ? parentName
                    : $"{parentAccountNode.Code} - {parentName}";

                parentAccountTreeNodes.Add(CloneParentTreeNode(parentAccountNode, 0));
            }
            else
            {
                foreach (var root in rootNodes)
                {
                    parentAccountTreeNodes.Add(CloneParentTreeNode(root, 0));
                }
            }

            return new DashboardTreeComputationResult
            {
                RootNodes = rootNodes,
                NodesById = nodes,
                TotalsByType = totals,
                BaseCurrency = baseCurrency,
                SelectedCurrency = selectedCurrency,
                CashBoxTree = cashBoxTreeNodes,
                CashBoxParentAccountConfigured = cashBoxParentConfigured,
                ParentAccountTree = parentAccountTreeNodes,
                ParentAccountConfigured = parentAccountConfigured,
                SelectedParentAccountName = selectedParentAccountName,
                StartDate = startDate,
                EndDate = endDate
            };
        }

        private class DashboardTreeComputationResult
        {
            public List<AccountTreeNodeViewModel> RootNodes { get; set; } = new List<AccountTreeNodeViewModel>();
            public Dictionary<int, AccountTreeNodeViewModel> NodesById { get; set; } = new Dictionary<int, AccountTreeNodeViewModel>();
            public Dictionary<AccountType, (decimal Selected, decimal Base)> TotalsByType { get; set; } = new Dictionary<AccountType, (decimal Selected, decimal Base)>();
            public Currency BaseCurrency { get; set; } = null!;
            public Currency SelectedCurrency { get; set; } = null!;
            public List<AccountTreeNodeViewModel> CashBoxTree { get; set; } = new List<AccountTreeNodeViewModel>();
            public bool CashBoxParentAccountConfigured { get; set; }
            public List<AccountTreeNodeViewModel> ParentAccountTree { get; set; } = new List<AccountTreeNodeViewModel>();
            public bool ParentAccountConfigured { get; set; }
            public string? SelectedParentAccountName { get; set; }
            public DateTime StartDate { get; set; }
            public DateTime EndDate { get; set; }
        }
    }
}

