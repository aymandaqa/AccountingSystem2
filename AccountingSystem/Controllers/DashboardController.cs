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

        private sealed record RoadUserInfo(string? FirstName, string? LastName, string? UserName, string? Mobile, int? BranchId);
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

            var branchMetadata = await _context.Branches
                .AsNoTracking()
                .Select(b => new
                {
                    b.Id,
                    b.Code,
                    b.NameAr,
                    b.NameEn,
                    b.IsActive
                })
                .ToListAsync();

            var branchInfoById = branchMetadata.ToDictionary(
                b => b.Id,
                b => new
                {
                    Code = string.IsNullOrWhiteSpace(b.Code) ? null : b.Code.Trim(),
                    DisplayName = !string.IsNullOrWhiteSpace(b.NameAr)
                        ? b.NameAr.Trim()
                        : (!string.IsNullOrWhiteSpace(b.NameEn) ? b.NameEn!.Trim() : $"فرع {b.Id}")
                });

            string? ResolveBranchCode(int? id)
            {
                if (!id.HasValue)
                {
                    return null;
                }

                return branchInfoById.TryGetValue(id.Value, out var info) ? info.Code : null;
            }

            string? ResolveBranchDisplayName(int? id)
            {
                if (!id.HasValue)
                {
                    return null;
                }

                return branchInfoById.TryGetValue(id.Value, out var info) ? info.DisplayName : null;
            }

            var treeData = await ComputeDashboardTreeAsync(userBranchIds, branchId, fromDate, toDate, currencyId);

            var allowedBranchIds = branchId.HasValue
                ? new List<int> { branchId.Value }
                : new List<int>(userBranchIds);

            var driverCodSummaries = await GetDriverCodBranchSummariesAsync(allowedBranchIds);
            var businessShipmentBranches = await GetBusinessShipmentBranchSummariesAsync(allowedBranchIds);
            // Always display customer account balances across all branches, not just the user's assigned branches.
            var customerAccountBranches = await GetCustomerAccountBranchesAsync(new List<int>(), treeData.BaseCurrency);

            string BuildAggregateKey(int? branchId, int? roadBranchId, string? branchName)
            {
                if (branchId.HasValue)
                {
                    return $"branch-{branchId.Value}";
                }

                if (roadBranchId.HasValue)
                {
                    return $"road-{roadBranchId.Value}";
                }

                var normalizedName = string.IsNullOrWhiteSpace(branchName) ? "غير معروف" : branchName.Trim();
                return $"name-{normalizedName}";
            }

            var branchAggregates = new Dictionary<string, BranchFinancialAggregateViewModel>();

            BranchFinancialAggregateViewModel GetOrCreateAggregate(int? branchId, int? roadBranchId, string? branchName, string? branchCode)
            {
                string key;

                if (!string.IsNullOrWhiteSpace(branchCode))
                {
                    key = $"code-{branchCode.Trim()}";
                }
                else
                {
                    key = BuildAggregateKey(branchId, roadBranchId, branchName);
                }

                if (!branchAggregates.TryGetValue(key, out var aggregate))
                {
                    var resolvedName = !string.IsNullOrWhiteSpace(branchName)
                        ? branchName!.Trim()
                        : (ResolveBranchDisplayName(branchId) ?? "غير محدد");

                    aggregate = new BranchFinancialAggregateViewModel
                    {
                        BranchId = branchId,
                        RoadCompanyBranchId = roadBranchId,
                        BranchName = resolvedName,
                        BranchCode = !string.IsNullOrWhiteSpace(branchCode)
                            ? branchCode.Trim()
                            : ResolveBranchCode(branchId)
                    };

                    branchAggregates[key] = aggregate;
                }
                else
                {
                    if (aggregate.BranchId is null && branchId.HasValue)
                    {
                        aggregate.BranchId = branchId;
                    }

                    if (aggregate.RoadCompanyBranchId is null && roadBranchId.HasValue)
                    {
                        aggregate.RoadCompanyBranchId = roadBranchId;
                    }

                    if (string.IsNullOrWhiteSpace(aggregate.BranchName) && !string.IsNullOrWhiteSpace(branchName))
                    {
                        aggregate.BranchName = branchName.Trim();
                    }
                    else if (string.IsNullOrWhiteSpace(aggregate.BranchName))
                    {
                        var resolvedName = ResolveBranchDisplayName(branchId);
                        if (!string.IsNullOrWhiteSpace(resolvedName))
                        {
                            aggregate.BranchName = resolvedName!;
                        }
                    }

                    if (string.IsNullOrWhiteSpace(aggregate.BranchCode))
                    {
                        var resolvedCode = !string.IsNullOrWhiteSpace(branchCode)
                            ? branchCode.Trim()
                            : ResolveBranchCode(branchId);

                        if (!string.IsNullOrWhiteSpace(resolvedCode))
                        {
                            aggregate.BranchCode = resolvedCode;
                        }
                    }
                }

                return aggregate;
            }

            foreach (var driverBranch in driverCodSummaries)
            {
                var branchName = string.IsNullOrWhiteSpace(driverBranch.BranchName)
                    ? ResolveBranchDisplayName(driverBranch.BranchId)
                    : driverBranch.BranchName;
                var aggregate = GetOrCreateAggregate(driverBranch.BranchId, null, branchName, ResolveBranchCode(driverBranch.BranchId));
                aggregate.DriverShipmentTotal += driverBranch.ShipmentTotal;
                aggregate.DriverCodAmount += driverBranch.ShipmentCod;
            }

            foreach (var customerBranch in customerAccountBranches)
            {
                var branchName = string.IsNullOrWhiteSpace(customerBranch.BranchName)
                    ? ResolveBranchDisplayName(customerBranch.BranchId)
                    : customerBranch.BranchName;
                var aggregate = GetOrCreateAggregate(customerBranch.BranchId, null, branchName, ResolveBranchCode(customerBranch.BranchId));
                aggregate.CustomerBalanceBase += customerBranch.TotalBalanceBase;
            }

            foreach (var shipmentBranch in businessShipmentBranches)
            {
                var branchName = string.IsNullOrWhiteSpace(shipmentBranch.BranchName)
                    ? ResolveBranchDisplayName(shipmentBranch.BranchId)
                    : shipmentBranch.BranchName;
                var aggregate = GetOrCreateAggregate(
                    shipmentBranch.BranchId,
                    shipmentBranch.RoadCompanyBranchId,
                    branchName,
                    ResolveBranchCode(shipmentBranch.BranchId));
                aggregate.SupplierShipmentCount += shipmentBranch.ShipmentCount;
                aggregate.SuppliersInTransit += shipmentBranch.TotalShipmentPrice;
            }

            var branchFinancialSummaries = branchAggregates.Values
                .OrderBy(a => string.IsNullOrWhiteSpace(a.BranchCode) ? a.BranchName : a.BranchCode)
                .ThenBy(a => a.BranchName)
                .ToList();

            var totalAccountBalancesBase = treeData.TotalsByType.TryGetValue(AccountType.Assets, out var assetTotals)
                ? assetTotals.Base
                : treeData.RootNodes
                    .Where(n => n.AccountType == AccountType.Assets)
                    .Sum(n => n.BalanceBase);
            var totalCustomerAccountBalancesBase = customerAccountBranches.Sum(b => b.TotalBalanceBase);
            var totalDriverCodCollection = driverCodSummaries.Sum(s => s.ShipmentCod);
            var totalSuppliersInTransit = businessShipmentBranches.Sum(s => s.TotalShipmentPrice);

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
                DriverCodBranchSummaries = driverCodSummaries,
                BusinessShipmentBranchSummaries = businessShipmentBranches,
                CustomerAccountBranches = customerAccountBranches,
                BranchFinancialSummaries = branchFinancialSummaries,
                TotalAccountBalancesSumBase = totalAccountBalancesBase,
                TotalCustomerAccountBalancesSumBase = totalCustomerAccountBalancesBase,
                NetAccountsAfterCustomersBase = totalAccountBalancesBase - totalCustomerAccountBalancesBase,
                TotalDriverCodCollection = totalDriverCodCollection,
                TotalSuppliersInTransit = totalSuppliersInTransit,
                NetDriverCodAfterSuppliers = totalDriverCodCollection - totalSuppliersInTransit
            };

            viewModel.NetIncome = viewModel.TotalRevenues - viewModel.TotalExpenses;
            viewModel.NetIncomeBase = viewModel.TotalRevenuesBase - viewModel.TotalExpensesBase;

            ViewBag.Branches = branchMetadata
                .Where(b => b.IsActive && userBranchIds.Contains(b.Id))
                .Select(b => new
                {
                    b.Id,
                    b.NameAr,
                    b.NameEn,
                    Name = ResolveBranchDisplayName(b.Id) ?? (string.IsNullOrWhiteSpace(b.Code) ? $"فرع {b.Id}" : b.Code!.Trim())
                })
                .ToList();

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

            if (userBranchIds.Any() && !userBranchIds.Contains(branchId))
            {
                return PartialView("~/Views/Dashboard/_DriverCodBranchDetails.cshtml", new List<DriverCodBranchDetailViewModel>());
            }

            var details = await GetDriverCodBranchDetailsAsync(branchId);

            return PartialView("~/Views/Dashboard/_DriverCodBranchDetails.cshtml", details);
        }

        [HttpGet]
        public async Task<IActionResult> LoadBusinessShipmentPriceDetails(int branchId)
        {
            if (branchId <= 0)
            {
                return PartialView("~/Views/Dashboard/_BusinessShipmentPricesTable.cshtml", new List<BusinessShipmentPriceViewModel>());
            }

            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == userId)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            if (userBranchIds.Any() && !userBranchIds.Contains(branchId))
            {
                return PartialView("~/Views/Dashboard/_BusinessShipmentPricesTable.cshtml", new List<BusinessShipmentPriceViewModel>());
            }

            var shipments = await GetBusinessShipmentPriceDetailsAsync(branchId);

            return PartialView("~/Views/Dashboard/_BusinessShipmentPricesTable.cshtml", shipments);
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

        private async Task<List<BusinessShipmentBranchSummaryViewModel>> GetBusinessShipmentBranchSummariesAsync(List<int> allowedBranchIds)
        {
            var results = new List<BusinessShipmentBranchSummaryViewModel>();
            var restrictBranches = allowedBranchIds?.Any() == true;

            try
            {
                var branchMetadata = await _context.Branches
                    .AsNoTracking()
                    .Select(b => new { b.Id, b.NameAr, b.Code })
                    .ToListAsync();

                var branchByRoadId = new Dictionary<int, (int BranchId, string BranchName)>();

                foreach (var branch in branchMetadata)
                {
                    if (int.TryParse(branch.Code, out var parsedCode))
                    {
                        var resolvedName = !string.IsNullOrWhiteSpace(branch.NameAr) ? branch.NameAr : $"فرع {branch.Id}";
                        branchByRoadId[parsedCode] = (branch.Id, resolvedName);
                    }
                }

                var grouped = await _roadContext.BusinessPayShipmentPrices
                    .AsNoTracking()
                    .GroupBy(s => s.CompanyBranchId)
                    .Select(g => new
                    {
                        CompanyBranchId = g.Key,
                        TotalShipmentPrice = g.Sum(s => s.ShipmentPrice ?? 0m),
                        ShipmentCount = g.Count()
                    })
                    .ToListAsync();

                foreach (var item in grouped)
                {
                    int? branchId = null;
                    string branchName = "فرع غير محدد";

                    if (item.CompanyBranchId.HasValue && branchByRoadId.TryGetValue(item.CompanyBranchId.Value, out var mappedBranch))
                    {
                        branchId = mappedBranch.BranchId;
                        branchName = mappedBranch.BranchName;
                    }
                    else if (item.CompanyBranchId.HasValue)
                    {
                        branchName = $"فرع Road #{item.CompanyBranchId.Value}";
                    }

                    if (restrictBranches && (!branchId.HasValue || !allowedBranchIds.Contains(branchId.Value)))
                    {
                        continue;
                    }

                    results.Add(new BusinessShipmentBranchSummaryViewModel
                    {
                        BranchId = branchId,
                        RoadCompanyBranchId = item.CompanyBranchId,
                        BranchName = branchName,
                        TotalShipmentPrice = item.TotalShipmentPrice,
                        ShipmentCount = item.ShipmentCount
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load business shipment branch summaries");
            }

            return results
                .OrderByDescending(r => r.TotalShipmentPrice)
                .ThenBy(r => r.BranchName)
                .ToList();
        }

        private async Task<List<BusinessShipmentPriceViewModel>> GetBusinessShipmentPriceDetailsAsync(int branchId)
        {
            const int maxResults = 200;
            var results = new List<BusinessShipmentPriceViewModel>();

            try
            {
                var branch = await _context.Branches
                    .AsNoTracking()
                    .Select(b => new { b.Id, b.NameAr, b.Code })
                    .FirstOrDefaultAsync(b => b.Id == branchId);

                if (branch == null || !int.TryParse(branch.Code, out var roadBranchId))
                {
                    return results;
                }

                var shipments = await _roadContext.BusinessPayShipmentPrices
                    .AsNoTracking()
                    .Where(s => s.CompanyBranchId == roadBranchId)
                    .OrderByDescending(s => s.EntryDate ?? DateTime.MinValue)
                    .ThenByDescending(s => s.Id)
                    .Take(maxResults)
                    .ToListAsync();

                var branchName = !string.IsNullOrWhiteSpace(branch.NameAr) ? branch.NameAr : $"فرع {branch.Id}";

                foreach (var shipment in shipments)
                {
                    results.Add(new BusinessShipmentPriceViewModel
                    {
                        Id = shipment.Id,
                        ShipmentTrackingNo = shipment.ShipmentTrackingNo,
                        ShipmentId = shipment.ShipmentId,
                        EntryDate = shipment.EntryDate,
                        BusinessId = shipment.BusinessId,
                        BusinessName = !string.IsNullOrWhiteSpace(shipment.BusinessName) ? shipment.BusinessName! : "غير محدد",
                        ClientName = shipment.ClientName ?? string.Empty,
                        CityName = shipment.CityName ?? string.Empty,
                        AreaName = shipment.AreaName ?? string.Empty,
                        ShipmentPrice = shipment.ShipmentPrice ?? 0m,
                        Status = shipment.Status ?? string.Empty,
                        DriverId = shipment.DriverId,
                        CompanyBranchId = shipment.CompanyBranchId,
                        BranchName = branchName,
                        BranchId = branch.Id
                    });
                }
            }
            catch (Exception ex)
            {
                _logger.LogError(ex, "Failed to load business shipment price details for branch {BranchId}", branchId);
            }

            return results
                .OrderByDescending(r => r.EntryDate ?? DateTime.MinValue)
                .ThenByDescending(r => r.Id)
                .ToList();
        }

        private async Task<List<CustomerBranchAccountNode>> GetCustomerAccountBranchesAsync(List<int> allowedBranchIds, Currency baseCurrency)
        {
            var normalizedBranchIds = allowedBranchIds?.Distinct().ToList() ?? new List<int>();
            var restrictBranches = normalizedBranchIds.Any();

            var customerMappings = await _context.CusomerMappingAccounts
                .AsNoTracking()
                .Where(m => !string.IsNullOrWhiteSpace(m.AccountCode))
                .ToListAsync();

            if (!customerMappings.Any())
            {
                return new List<CustomerBranchAccountNode>();
            }

            decimal Round(decimal value) => Math.Round(value, 2, MidpointRounding.AwayFromZero);

            var mappingByCustomer = customerMappings
                .Select(m => new
                {
                    Mapping = m,
                    CustomerId = int.TryParse(m.CustomerId, out var parsedCustomerId) ? parsedCustomerId : (int?)null
                })
                .ToList();

            var accountCodes = customerMappings
                .Select(m => m.AccountCode!)
                .Distinct()
                .ToList();

            IQueryable<Account> accountsQuery = _context.Accounts
                .AsNoTracking()
                .Where(a => accountCodes.Contains(a.Code!));

            if (restrictBranches)
            {
                accountsQuery = accountsQuery.Where(a => !a.BranchId.HasValue || normalizedBranchIds.Contains(a.BranchId.Value));
            }

            accountsQuery = accountsQuery
                .Include(a => a.Currency)
                .Include(a => a.Branch);

            var accounts = await accountsQuery.ToDictionaryAsync(a => a.Code!);

            var validCustomerIds = mappingByCustomer
                .Select(x => x.CustomerId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            Dictionary<int, RoadUserInfo> customers = validCustomerIds.Any()
                ? await _roadContext.Users
                    .AsNoTracking()
                    .Where(u => validCustomerIds.Contains(u.Id))
                    .Select(u => new
                    {
                        u.Id,
                        u.FirstName,
                        u.LastName,
                        u.UserName,
                        u.MobileNo1,
                        u.CompanyBranchId
                    })
                    .ToDictionaryAsync(
                        u => u.Id,
                        u => new RoadUserInfo(u.FirstName, u.LastName, u.UserName, u.MobileNo1, u.CompanyBranchId))
                : new Dictionary<int, RoadUserInfo>();

            var roadBranchIds = customers.Values
                .Select(c => c.BranchId)
                .Where(id => id.HasValue)
                .Select(id => id!.Value)
                .Distinct()
                .ToList();

            Dictionary<int, string> roadBranches = roadBranchIds.Any()
                ? await _roadContext.CompanyBranches
                    .AsNoTracking()
                    .Where(b => roadBranchIds.Contains(b.Id))
                    .ToDictionaryAsync(b => b.Id, b => b.BranchName ?? $"فرع {b.Id}")
                : new Dictionary<int, string>();

            var branchMap = new Dictionary<string, CustomerBranchAccountNode>();

            foreach (var entry in mappingByCustomer)
            {
                if (!accounts.TryGetValue(entry.Mapping.AccountCode!, out var account))
                {
                    continue;
                }

                var balanceBase = Round(_currencyService.Convert(account.CurrentBalance, account.Currency, baseCurrency));
                var balanceOriginal = Round(account.CurrentBalance);
                var accountCurrencyCode = account.Currency.Code;

                string customerName = !string.IsNullOrWhiteSpace(entry.Mapping.CustomerId)
                    ? $"عميل #{entry.Mapping.CustomerId}"
                    : "عميل غير معروف";
                string? customerContact = null;

                int? accountBranchId = account.BranchId;
                int? customerBranchId = null;

                if (entry.CustomerId.HasValue && customers.TryGetValue(entry.CustomerId.Value, out var customer))
                {
                    var nameParts = new List<string>();
                    if (!string.IsNullOrWhiteSpace(customer.FirstName))
                    {
                        nameParts.Add(customer.FirstName!);
                    }

                    if (!string.IsNullOrWhiteSpace(customer.LastName))
                    {
                        nameParts.Add(customer.LastName!);
                    }

                    if (nameParts.Any())
                    {
                        customerName = string.Join(" ", nameParts);
                    }
                    else if (!string.IsNullOrWhiteSpace(customer.UserName))
                    {
                        customerName = customer.UserName!;
                    }

                    if (!string.IsNullOrWhiteSpace(customer.Mobile))
                    {
                        customerContact = customer.Mobile;
                    }

                    customerBranchId = customer.BranchId;
                }

                int? branchIdForCustomer = null;

                if (restrictBranches)
                {
                    if (accountBranchId.HasValue && normalizedBranchIds.Contains(accountBranchId.Value))
                    {
                        branchIdForCustomer = accountBranchId.Value;
                    }
                    else if (customerBranchId.HasValue && normalizedBranchIds.Contains(customerBranchId.Value))
                    {
                        branchIdForCustomer = customerBranchId.Value;
                    }
                    else if (accountBranchId.HasValue || customerBranchId.HasValue)
                    {
                        continue;
                    }
                }
                else
                {
                    branchIdForCustomer = customerBranchId ?? accountBranchId;
                }

                var branchKey = branchIdForCustomer.HasValue
                    ? $"branch-{branchIdForCustomer.Value}"
                    : "branch-unassigned";

                string branchName;
                if (branchIdForCustomer.HasValue)
                {
                    if (branchIdForCustomer == accountBranchId && account.Branch != null)
                    {
                        branchName = !string.IsNullOrWhiteSpace(account.Branch.NameAr)
                            ? account.Branch.NameAr!
                            : account.Branch.NameEn ?? $"فرع {branchIdForCustomer.Value}";
                    }
                    else if (branchIdForCustomer == customerBranchId && customerBranchId.HasValue && roadBranches.TryGetValue(customerBranchId.Value, out var roadBranchName))
                    {
                        branchName = roadBranchName;
                    }
                    else
                    {
                        branchName = account.Branch != null
                            ? (!string.IsNullOrWhiteSpace(account.Branch.NameAr)
                                ? account.Branch.NameAr!
                                : account.Branch.NameEn ?? $"فرع {branchIdForCustomer.Value}")
                            : $"فرع {branchIdForCustomer.Value}";
                    }
                }
                else
                {
                    branchName = account.Branch != null
                        ? (!string.IsNullOrWhiteSpace(account.Branch.NameAr)
                            ? account.Branch.NameAr!
                            : account.Branch.NameEn ?? "فرع غير محدد")
                        : "فرع غير محدد";
                }

                if (!branchMap.TryGetValue(branchKey, out var branchNode))
                {
                    branchNode = new CustomerBranchAccountNode
                    {
                        BranchId = branchIdForCustomer,
                        BranchName = branchName
                    };
                    branchMap[branchKey] = branchNode;
                }

                branchNode.TotalBalanceBase += balanceBase;
                branchNode.Customers.Add(new CustomerAccountBalanceNode
                {
                    CustomerId = entry.Mapping.CustomerId ?? string.Empty,
                    CustomerName = customerName,
                    CustomerContact = customerContact,
                    AccountCode = account.Code ?? string.Empty,
                    AccountName = !string.IsNullOrWhiteSpace(account.NameAr) ? account.NameAr! : account.NameEn ?? account.Code ?? string.Empty,
                    BalanceBase = balanceBase,
                    BalanceOriginal = balanceOriginal,
                    AccountCurrencyCode = accountCurrencyCode
                });
            }

            if (restrictBranches && normalizedBranchIds.Any())
            {
                var existingBranchIds = branchMap.Values
                    .Where(b => b.BranchId.HasValue)
                    .Select(b => b.BranchId!.Value)
                    .ToHashSet();

                var missingBranchIds = normalizedBranchIds
                    .Where(id => !existingBranchIds.Contains(id))
                    .ToList();

                if (missingBranchIds.Any())
                {
                    var branchInfos = await _context.Branches
                        .AsNoTracking()
                        .Where(b => missingBranchIds.Contains(b.Id))
                        .Select(b => new { b.Id, b.NameAr, b.NameEn })
                        .ToDictionaryAsync(b => b.Id);

                    foreach (var missingBranchId in missingBranchIds)
                    {
                        var branchInfo = branchInfos.TryGetValue(missingBranchId, out var info) ? info : null;
                        var branchName = branchInfo != null
                            ? (!string.IsNullOrWhiteSpace(branchInfo.NameAr)
                                ? branchInfo.NameAr!
                                : branchInfo.NameEn ?? $"فرع {missingBranchId}")
                            : $"فرع {missingBranchId}";

                        branchMap[$"branch-{missingBranchId}"] = new CustomerBranchAccountNode
                        {
                            BranchId = missingBranchId,
                            BranchName = branchName
                        };
                    }
                }
            }

            foreach (var branchNode in branchMap.Values)
            {
                branchNode.TotalBalanceBase = Round(branchNode.TotalBalanceBase);
                branchNode.Customers = branchNode.Customers
                    .OrderByDescending(c => Math.Abs(c.BalanceBase))
                    .ThenBy(c => c.CustomerName)
                    .ToList();
            }

            return branchMap.Values
                .OrderByDescending(b => Math.Abs(b.TotalBalanceBase))
                .ThenBy(b => b.BranchName)
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

