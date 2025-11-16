using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.Workflows;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
    [Authorize]
    public class TransactionsController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IAuthorizationService _authorizationService;

        public TransactionsController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IAuthorizationService authorizationService)
        {
            _context = context;
            _userManager = userManager;
            _authorizationService = authorizationService;
        }

        public async Task<IActionResult> Index(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchTerm = null,
            int? branchId = null,
            int page = 1,
            int pageSize = 25)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            if (!await UserHasTransactionAccessAsync())
            {
                return Forbid();
            }

            var userBranchIds = await GetUserBranchIdsAsync(user.Id);

            var normalizedPageSize = pageSize <= 0 ? 25 : Math.Min(pageSize, 100);
            var currentPage = Math.Max(page, 1);

            var transactions = new List<TransactionListItemViewModel>();

            transactions.AddRange(await GetPaymentVoucherTransactionsAsync(user, userBranchIds, fromDate, toDate, branchId));
            transactions.AddRange(await GetReceiptVoucherTransactionsAsync(user, userBranchIds, fromDate, toDate, branchId));
            transactions.AddRange(await GetDisbursementVoucherTransactionsAsync(user, userBranchIds, fromDate, toDate, branchId));
            transactions.AddRange(await GetAssetExpenseTransactionsAsync(user, userBranchIds, fromDate, toDate, branchId));

            var normalizedSearch = searchTerm?.Trim();
            if (!string.IsNullOrWhiteSpace(normalizedSearch))
            {
                var comparison = StringComparison.OrdinalIgnoreCase;
                transactions = transactions
                    .Where(t =>
                        t.Type.Contains(normalizedSearch, comparison) ||
                        (!string.IsNullOrWhiteSpace(t.Counterparty) && t.Counterparty.Contains(normalizedSearch, comparison)) ||
                        (!string.IsNullOrWhiteSpace(t.BranchName) && t.BranchName.Contains(normalizedSearch, comparison)) ||
                        (!string.IsNullOrWhiteSpace(t.CurrencyCode) && t.CurrencyCode.Contains(normalizedSearch, comparison)) ||
                        (!string.IsNullOrWhiteSpace(t.Status) && t.Status.Contains(normalizedSearch, comparison)) ||
                        (!string.IsNullOrWhiteSpace(t.Notes) && t.Notes.Contains(normalizedSearch, comparison)) ||
                        t.Id.ToString(CultureInfo.InvariantCulture).Contains(normalizedSearch, comparison) ||
                        t.Amount.ToString(CultureInfo.InvariantCulture).Contains(normalizedSearch, comparison))
                    .ToList();
            }

            var orderedTransactions = transactions
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.Id)
                .ToList();

            var totalCount = orderedTransactions.Count;
            var pagedItems = orderedTransactions
                .Skip((currentPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToList();

            var branchesQuery = _context.Branches.AsNoTracking();

            if (userBranchIds.Any())
            {
                branchesQuery = branchesQuery.Where(b => userBranchIds.Contains(b.Id));
            }
            else if (user.PaymentBranchId.HasValue)
            {
                branchesQuery = branchesQuery.Where(b => b.Id == user.PaymentBranchId.Value);
            }

            var branchOptions = await branchesQuery
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = string.IsNullOrWhiteSpace(b.NameAr) ? b.NameEn ?? b.Code : b.NameAr
                })
                .ToListAsync();

            ViewBag.UserBranches = branchOptions;
            ViewBag.SelectedBranchId = branchId;

            var model = new PaginatedListViewModel<TransactionListItemViewModel>
            {
                Items = pagedItems,
                TotalCount = totalCount,
                PageIndex = currentPage,
                PageSize = normalizedPageSize,
                SearchTerm = searchTerm,
                FromDate = fromDate,
                ToDate = toDate
            };

            return View(model);
        }

        private async Task<bool> UserHasTransactionAccessAsync()
        {
            var policies = new[]
            {
                "paymentvouchers.view",
                "receiptvouchers.view",
                "disbursementvouchers.view",
                "assetexpenses.view"
            };

            foreach (var policy in policies)
            {
                var result = await _authorizationService.AuthorizeAsync(User, policy);
                if (result.Succeeded)
                {
                    return true;
                }
            }

            return false;
        }

        private async Task<List<int>> GetUserBranchIdsAsync(string userId)
        {
            return await _context.UserBranches
                .Where(ub => ub.UserId == userId)
                .Select(ub => ub.BranchId)
                .ToListAsync();
        }

        private IQueryable<PaymentVoucher> BuildPaymentVoucherQuery(User user, List<int> userBranchIds, DateTime? fromDate, DateTime? toDate, int? branchId)
        {
            var query = _context.PaymentVouchers
                .Include(v => v.Supplier)
                .Include(v => v.Agent)
                .Include(v => v.Account)
                .Include(v => v.Currency)
                .Include(v => v.CreatedBy)
                    .ThenInclude(u => u.PaymentBranch)
                .AsNoTracking();

            if (userBranchIds.Any())
            {
                query = query.Where(v => v.CreatedBy.PaymentBranchId.HasValue && userBranchIds.Contains(v.CreatedBy.PaymentBranchId.Value));
            }
            else if (user.PaymentBranchId.HasValue)
            {
                query = query.Where(v => v.CreatedBy.PaymentBranchId == user.PaymentBranchId);
            }
            else
            {
                query = query.Where(v => v.CreatedById == user.Id);
            }

            if (branchId.HasValue)
            {
                query = query.Where(v => v.CreatedBy.PaymentBranchId.HasValue && v.CreatedBy.PaymentBranchId.Value == branchId.Value);
            }

            if (fromDate.HasValue)
            {
                var startDate = fromDate.Value.Date;
                query = query.Where(v => v.Date >= startDate);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1);
                query = query.Where(v => v.Date < endDate);
            }

            return query;
        }

        private IQueryable<ReceiptVoucher> BuildReceiptVoucherQuery(User user, List<int> userBranchIds, DateTime? fromDate, DateTime? toDate, int? branchId)
        {
            var query = _context.ReceiptVouchers
                .Include(v => v.Supplier)
                .Include(v => v.Account)
                .Include(v => v.Currency)
                .Include(v => v.CreatedBy)
                    .ThenInclude(u => u.PaymentBranch)
                .AsNoTracking();

            if (userBranchIds.Any())
            {
                query = query.Where(v => v.CreatedBy.PaymentBranchId.HasValue && userBranchIds.Contains(v.CreatedBy.PaymentBranchId.Value));
            }
            else if (user.PaymentBranchId.HasValue)
            {
                query = query.Where(v => v.CreatedBy.PaymentBranchId == user.PaymentBranchId);
            }
            else
            {
                query = query.Where(v => v.CreatedById == user.Id);
            }

            if (branchId.HasValue)
            {
                query = query.Where(v => v.CreatedBy.PaymentBranchId.HasValue && v.CreatedBy.PaymentBranchId.Value == branchId.Value);
            }

            if (fromDate.HasValue)
            {
                var startDate = fromDate.Value.Date;
                query = query.Where(v => v.Date >= startDate);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1);
                query = query.Where(v => v.Date < endDate);
            }

            return query;
        }

        private IQueryable<DisbursementVoucher> BuildDisbursementVoucherQuery(User user, List<int> userBranchIds, DateTime? fromDate, DateTime? toDate, int? branchId)
        {
            var query = _context.DisbursementVouchers
                .Include(v => v.Supplier)
                .Include(v => v.Currency)
                .Include(v => v.CreatedBy)
                    .ThenInclude(u => u.PaymentBranch)
                .AsNoTracking();

            if (userBranchIds.Any())
            {
                query = query.Where(v => v.CreatedBy.PaymentBranchId.HasValue && userBranchIds.Contains(v.CreatedBy.PaymentBranchId.Value));
            }
            else if (user.PaymentBranchId.HasValue)
            {
                query = query.Where(v => v.CreatedBy.PaymentBranchId == user.PaymentBranchId);
            }
            else
            {
                query = query.Where(v => v.CreatedById == user.Id);
            }

            if (branchId.HasValue)
            {
                query = query.Where(v => v.CreatedBy.PaymentBranchId.HasValue && v.CreatedBy.PaymentBranchId.Value == branchId.Value);
            }

            if (fromDate.HasValue)
            {
                var startDate = fromDate.Value.Date;
                query = query.Where(v => v.Date >= startDate);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1);
                query = query.Where(v => v.Date < endDate);
            }

            return query;
        }

        private IQueryable<AssetExpense> BuildAssetExpenseQuery(User user, List<int> userBranchIds, DateTime? fromDate, DateTime? toDate, int? branchId)
        {
            var query = _context.AssetExpenses
                .Include(e => e.Asset)
                    .ThenInclude(a => a.Branch)
                .Include(e => e.Supplier)
                .Include(e => e.Currency)
                .Include(e => e.CreatedBy)
                .Include(e => e.WorkflowInstance)
                .AsNoTracking();

            if (userBranchIds.Any())
            {
                query = query.Where(e => userBranchIds.Contains(e.Asset.BranchId));
            }
            else if (user.PaymentBranchId.HasValue)
            {
                query = query.Where(e => e.Asset.BranchId == user.PaymentBranchId.Value);
            }
            else
            {
                query = query.Where(e => e.CreatedById == user.Id);
            }

            if (branchId.HasValue)
            {
                query = query.Where(e => e.Asset.BranchId == branchId.Value);
            }

            if (fromDate.HasValue)
            {
                var startDate = fromDate.Value.Date;
                query = query.Where(e => e.Date >= startDate);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1);
                query = query.Where(e => e.Date < endDate);
            }

            return query;
        }

        private async Task<IEnumerable<TransactionListItemViewModel>> GetPaymentVoucherTransactionsAsync(
            User user,
            List<int> userBranchIds,
            DateTime? fromDate,
            DateTime? toDate,
            int? branchId)
        {
            var query = BuildPaymentVoucherQuery(user, userBranchIds, fromDate, toDate, branchId);

            return await query.Select(v => new TransactionListItemViewModel
            {
                Id = v.Id,
                Type = "سند صرف", 
                TypeKey = nameof(PaymentVoucher),
                Date = v.Date,
                BranchName = v.CreatedBy.PaymentBranch == null ? "-" : v.CreatedBy.PaymentBranch.NameAr ?? v.CreatedBy.PaymentBranch.NameEn ?? v.CreatedBy.PaymentBranch.Code,
                CurrencyCode = v.Currency.Code,
                Amount = v.Amount,
                Counterparty = v.Supplier != null
                    ? v.Supplier.NameAr
                    : v.Agent != null
                        ? v.Agent.Name
                        : v.Account != null
                            ? v.Account.NameAr
                            : string.Empty,
                Notes = v.Notes,
                Status = v.Status switch
                {
                    PaymentVoucherStatus.PendingApproval => "بانتظار الموافقة",
                    PaymentVoucherStatus.Approved => "معتمد",
                    PaymentVoucherStatus.Rejected => "مرفوض",
                    PaymentVoucherStatus.Draft => "مسودة",
                    _ => v.Status.ToString()
                },
                DetailsController = "PaymentVouchers"
            }).ToListAsync();
        }

        private async Task<IEnumerable<TransactionListItemViewModel>> GetReceiptVoucherTransactionsAsync(
            User user,
            List<int> userBranchIds,
            DateTime? fromDate,
            DateTime? toDate,
            int? branchId)
        {
            var query = BuildReceiptVoucherQuery(user, userBranchIds, fromDate, toDate, branchId);

            return await query.Select(v => new TransactionListItemViewModel
            {
                Id = v.Id,
                Type = "سند قبض",
                TypeKey = nameof(ReceiptVoucher),
                Date = v.Date,
                BranchName = v.CreatedBy.PaymentBranch == null ? "-" : v.CreatedBy.PaymentBranch.NameAr ?? v.CreatedBy.PaymentBranch.NameEn ?? v.CreatedBy.PaymentBranch.Code,
                CurrencyCode = v.Currency.Code,
                Amount = v.Amount,
                Counterparty = v.Supplier != null ? v.Supplier.NameAr : v.Account.NameAr,
                Notes = v.Notes,
                Status = v.Status switch
                {
                    ReceiptVoucherStatus.PendingApproval => "بانتظار الموافقة",
                    ReceiptVoucherStatus.Approved => "معتمد",
                    ReceiptVoucherStatus.Rejected => "مرفوض",
                    _ => v.Status.ToString()
                },
                DetailsController = "ReceiptVouchers"
            }).ToListAsync();
        }

        private async Task<IEnumerable<TransactionListItemViewModel>> GetDisbursementVoucherTransactionsAsync(
            User user,
            List<int> userBranchIds,
            DateTime? fromDate,
            DateTime? toDate,
            int? branchId)
        {
            var query = BuildDisbursementVoucherQuery(user, userBranchIds, fromDate, toDate, branchId);

            return await query.Select(v => new TransactionListItemViewModel
            {
                Id = v.Id,
                Type = "سند دفع (الصندوق)",
                TypeKey = nameof(DisbursementVoucher),
                Date = v.Date,
                BranchName = v.CreatedBy.PaymentBranch == null ? "-" : v.CreatedBy.PaymentBranch.NameAr ?? v.CreatedBy.PaymentBranch.NameEn ?? v.CreatedBy.PaymentBranch.Code,
                CurrencyCode = v.Currency.Code,
                Amount = v.Amount,
                Counterparty = v.Supplier.NameAr,
                Notes = v.Notes,
                Status = v.Status switch
                {
                    DisbursementVoucherStatus.PendingApproval => "بانتظار الموافقة",
                    DisbursementVoucherStatus.Approved => "معتمد",
                    DisbursementVoucherStatus.Rejected => "مرفوض",
                    _ => v.Status.ToString()
                },
                DetailsController = "DisbursementVouchers"
            }).ToListAsync();
        }

        private async Task<IEnumerable<TransactionListItemViewModel>> GetAssetExpenseTransactionsAsync(
            User user,
            List<int> userBranchIds,
            DateTime? fromDate,
            DateTime? toDate,
            int? branchId)
        {
            var query = BuildAssetExpenseQuery(user, userBranchIds, fromDate, toDate, branchId);

            return await query.Select(e => new TransactionListItemViewModel
            {
                Id = e.Id,
                Type = "مصروف أصل",
                TypeKey = nameof(AssetExpense),
                Date = e.Date,
                BranchName = e.Asset.Branch.NameAr,
                CurrencyCode = e.Currency.Code,
                Amount = e.Amount,
                Counterparty = e.Asset.Name,
                Notes = e.Notes,
                Status = (e.WorkflowInstance?.Status ?? WorkflowInstanceStatus.Approved) switch
                {
                    WorkflowInstanceStatus.InProgress => "قيد الاعتماد",
                    WorkflowInstanceStatus.Rejected => "مرفوض",
                    WorkflowInstanceStatus.Cancelled => "ملغي",
                    WorkflowInstanceStatus.Approved => "معتمد",
                    _ => "معتمد"
                },
                DetailsController = "AssetExpenses"
            }).ToListAsync();
        }
    }
}

