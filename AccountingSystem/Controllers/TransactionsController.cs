using System;
using System.Collections.Generic;
using System.Globalization;
using System.Linq;
using System.IO;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.Workflows;
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

            var orderedTransactions = await BuildTransactionsAsync(
                user,
                userBranchIds,
                fromDate,
                toDate,
                branchId,
                searchTerm);

            var totalCount = orderedTransactions.Count;
            var pagedItems = orderedTransactions
                .Skip((currentPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToList();

            var typeSummaries = orderedTransactions
                .GroupBy(t => t.Type)
                .Select(g => new TransactionSummaryItemViewModel
                {
                    Label = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(t => t.Amount)
                })
                .OrderByDescending(s => s.Count)
                .ThenBy(s => s.Label)
                .ToList();

            var statusSummaries = orderedTransactions
                .GroupBy(t => string.IsNullOrWhiteSpace(t.Status) ? "غير محدد" : t.Status!)
                .Select(g => new TransactionSummaryItemViewModel
                {
                    Label = g.Key,
                    Count = g.Count(),
                    TotalAmount = g.Sum(t => t.Amount)
                })
                .OrderByDescending(s => s.Count)
                .ThenBy(s => s.Label)
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

            var model = new TransactionIndexViewModel
            {
                Items = pagedItems,
                TotalCount = totalCount,
                PageIndex = currentPage,
                PageSize = normalizedPageSize,
                SearchTerm = searchTerm,
                FromDate = fromDate,
                ToDate = toDate,
                TypeSummaries = typeSummaries,
                StatusSummaries = statusSummaries
            };

            return View(model);
        }

        [HttpGet]
        public async Task<IActionResult> ExportExcel(
            DateTime? fromDate = null,
            DateTime? toDate = null,
            string? searchTerm = null,
            int? branchId = null)
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

            var transactions = await BuildTransactionsAsync(
                user,
                userBranchIds,
                fromDate,
                toDate,
                branchId,
                searchTerm);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Transactions");

            worksheet.Cell(1, 1).Value = "التاريخ";
            worksheet.Cell(1, 2).Value = "نوع الحركة";
            worksheet.Cell(1, 3).Value = "الطرف / البيان";
            worksheet.Cell(1, 4).Value = "المبلغ";
            worksheet.Cell(1, 5).Value = "العملة";
            worksheet.Cell(1, 6).Value = "الفرع";
            worksheet.Cell(1, 7).Value = "الحالة";
            worksheet.Cell(1, 8).Value = "المستخدم";
            worksheet.Cell(1, 9).Value = "ملاحظات";
            worksheet.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var transaction in transactions)
            {
                worksheet.Cell(row, 1).Value = transaction.Date;
                worksheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-MM-dd HH:mm";
                worksheet.Cell(row, 2).Value = transaction.Type;
                worksheet.Cell(row, 3).Value = transaction.Counterparty ?? string.Empty;
                worksheet.Cell(row, 4).Value = transaction.Amount;
                worksheet.Cell(row, 5).Value = transaction.CurrencyCode;
                worksheet.Cell(row, 6).Value = transaction.BranchName;
                worksheet.Cell(row, 7).Value = transaction.Status ?? string.Empty;
                worksheet.Cell(row, 8).Value = transaction.CreatedByName ?? string.Empty;
                worksheet.Cell(row, 9).Value = transaction.Notes ?? string.Empty;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Seek(0, SeekOrigin.Begin);

            var fileName = $"transactions_{DateTime.UtcNow:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpGet]
        public async Task<IActionResult> Details(string typeKey, int id)
        {
            if (string.IsNullOrWhiteSpace(typeKey))
            {
                return NotFound();
            }

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
            var transaction = await FindTransactionAsync(user, userBranchIds, typeKey, id);

            if (transaction == null)
            {
                return NotFound();
            }

            ViewBag.TransactionTypeKey = typeKey;
            return View(transaction);
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
                Status = v.Status == PaymentVoucherStatus.PendingApproval
                    ? "بانتظار الموافقة"
                    : v.Status == PaymentVoucherStatus.Approved
                        ? "معتمد"
                        : v.Status == PaymentVoucherStatus.Rejected
                            ? "مرفوض"
                            : v.Status == PaymentVoucherStatus.Draft
                                ? "مسودة"
                                : v.Status.ToString(),
                DetailsController = "PaymentVouchers",
                DetailsAction = "Details",
                CreatedByName = v.CreatedBy.FullName ?? v.CreatedBy.UserName
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
                Status = v.Status == ReceiptVoucherStatus.PendingApproval
                    ? "بانتظار الموافقة"
                    : v.Status == ReceiptVoucherStatus.Approved
                        ? "معتمد"
                        : v.Status == ReceiptVoucherStatus.Rejected
                            ? "مرفوض"
                            : v.Status.ToString(),
                DetailsController = "ReceiptVouchers",
                DetailsAction = "Details",
                CreatedByName = v.CreatedBy.FullName ?? v.CreatedBy.UserName
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
                Status = v.Status == DisbursementVoucherStatus.PendingApproval
                    ? "بانتظار الموافقة"
                    : v.Status == DisbursementVoucherStatus.Approved
                        ? "معتمد"
                        : v.Status == DisbursementVoucherStatus.Rejected
                            ? "مرفوض"
                            : v.Status.ToString(),
                DetailsController = "DisbursementVouchers",
                DetailsAction = "Details",
                CreatedByName = v.CreatedBy.FullName ?? v.CreatedBy.UserName
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
                Status = e.WorkflowInstance == null
                    ? "معتمد"
                    : e.WorkflowInstance.Status == WorkflowInstanceStatus.InProgress
                        ? "قيد الاعتماد"
                        : e.WorkflowInstance.Status == WorkflowInstanceStatus.Rejected
                            ? "مرفوض"
                            : e.WorkflowInstance.Status == WorkflowInstanceStatus.Cancelled
                                ? "ملغي"
                                : e.WorkflowInstance.Status == WorkflowInstanceStatus.Approved
                                    ? "معتمد"
                                    : "معتمد",
                DetailsController = "AssetExpenses",
                DetailsAction = "Details",
                CreatedByName = e.CreatedBy.FullName ?? e.CreatedBy.UserName
            }).ToListAsync();
        }

        private async Task<List<TransactionListItemViewModel>> BuildTransactionsAsync(
            User user,
            List<int> userBranchIds,
            DateTime? fromDate,
            DateTime? toDate,
            int? branchId,
            string? searchTerm)
        {
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
                        (!string.IsNullOrWhiteSpace(t.CreatedByName) && t.CreatedByName.Contains(normalizedSearch, comparison)) ||
                        t.Id.ToString(CultureInfo.InvariantCulture).Contains(normalizedSearch, comparison) ||
                        t.Amount.ToString(CultureInfo.InvariantCulture).Contains(normalizedSearch, comparison))
                    .ToList();
            }

            return transactions
                .OrderByDescending(t => t.Date)
                .ThenByDescending(t => t.Id)
                .ToList();
        }

        private async Task<TransactionListItemViewModel?> FindTransactionAsync(
            User user,
            List<int> userBranchIds,
            string typeKey,
            int id)
        {
            switch (typeKey)
            {
                case nameof(PaymentVoucher):
                    return await BuildPaymentVoucherQuery(user, userBranchIds, null, null, null)
                        .Where(v => v.Id == id)
                        .Select(v => new TransactionListItemViewModel
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
                            Status = v.Status == PaymentVoucherStatus.PendingApproval
                                ? "بانتظار الموافقة"
                                : v.Status == PaymentVoucherStatus.Approved
                                    ? "معتمد"
                                    : v.Status == PaymentVoucherStatus.Rejected
                                        ? "مرفوض"
                                        : v.Status == PaymentVoucherStatus.Draft
                                            ? "مسودة"
                                            : v.Status.ToString(),
                            DetailsController = "Transactions",
                            DetailsAction = "Details",
                            CreatedByName = v.CreatedBy.FullName ?? v.CreatedBy.UserName
                        })
                        .FirstOrDefaultAsync();
                case nameof(ReceiptVoucher):
                    return await BuildReceiptVoucherQuery(user, userBranchIds, null, null, null)
                        .Where(v => v.Id == id)
                        .Select(v => new TransactionListItemViewModel
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
                            Status = v.Status == ReceiptVoucherStatus.PendingApproval
                                ? "بانتظار الموافقة"
                                : v.Status == ReceiptVoucherStatus.Approved
                                    ? "معتمد"
                                    : v.Status == ReceiptVoucherStatus.Rejected
                                        ? "مرفوض"
                                        : v.Status.ToString(),
                            DetailsController = "Transactions",
                            DetailsAction = "Details",
                            CreatedByName = v.CreatedBy.FullName ?? v.CreatedBy.UserName
                        })
                        .FirstOrDefaultAsync();
                case nameof(DisbursementVoucher):
                    return await BuildDisbursementVoucherQuery(user, userBranchIds, null, null, null)
                        .Where(v => v.Id == id)
                        .Select(v => new TransactionListItemViewModel
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
                            Status = v.Status == DisbursementVoucherStatus.PendingApproval
                                ? "بانتظار الموافقة"
                                : v.Status == DisbursementVoucherStatus.Approved
                                    ? "معتمد"
                                    : v.Status == DisbursementVoucherStatus.Rejected
                                        ? "مرفوض"
                                        : v.Status.ToString(),
                            DetailsController = "Transactions",
                            DetailsAction = "Details",
                            CreatedByName = v.CreatedBy.FullName ?? v.CreatedBy.UserName
                        })
                        .FirstOrDefaultAsync();
                case nameof(AssetExpense):
                    return await BuildAssetExpenseQuery(user, userBranchIds, null, null, null)
                        .Where(e => e.Id == id)
                        .Select(e => new TransactionListItemViewModel
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
                            Status = e.WorkflowInstance == null
                                ? "معتمد"
                                : e.WorkflowInstance.Status == WorkflowInstanceStatus.InProgress
                                    ? "قيد الاعتماد"
                                    : e.WorkflowInstance.Status == WorkflowInstanceStatus.Rejected
                                        ? "مرفوض"
                                        : e.WorkflowInstance.Status == WorkflowInstanceStatus.Cancelled
                                            ? "ملغي"
                                            : e.WorkflowInstance.Status == WorkflowInstanceStatus.Approved
                                                ? "معتمد"
                                                : "معتمد",
                            DetailsController = "Transactions",
                            DetailsAction = "Details",
                            CreatedByName = e.CreatedBy.FullName ?? e.CreatedBy.UserName
                        })
                        .FirstOrDefaultAsync();
                default:
                    return null;
            }
        }
    }
}

