using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Models.Workflows;
using ClosedXML.Excel;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AccountingSystem.Extensions;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "disbursementvouchers.view")]
    public class DisbursementVouchersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWorkflowService _workflowService;
        private readonly IDisbursementVoucherProcessor _disbursementVoucherProcessor;
        private readonly IAttachmentStorageService _attachmentStorageService;

        public DisbursementVouchersController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IWorkflowService workflowService,
            IDisbursementVoucherProcessor disbursementVoucherProcessor,
            IAttachmentStorageService attachmentStorageService)
        {
            _context = context;
            _userManager = userManager;
            _workflowService = workflowService;
            _disbursementVoucherProcessor = disbursementVoucherProcessor;
            _attachmentStorageService = attachmentStorageService;
        }

        private async Task<List<int>> GetUserBranchIdsAsync(string userId)
        {
            return await _context.UserBranches
                .Where(ub => ub.UserId == userId)
                .Select(ub => ub.BranchId)
                .ToListAsync();
        }

        public async Task<IActionResult> Index(DateTime? fromDate = null, DateTime? toDate = null, string? searchTerm = null, int? branchId = null, int page = 1, int pageSize = 25)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == user.Id)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            var normalizedPageSize = pageSize <= 0 ? 25 : Math.Min(pageSize, 100);
            var currentPage = Math.Max(page, 1);

            var vouchersQuery = BuildQuery(user, userBranchIds, fromDate, toDate, branchId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                vouchersQuery = ApplySearchFilters(vouchersQuery, searchTerm.Trim());
            }

            var totalCount = await vouchersQuery.CountAsync();

            var vouchers = await vouchersQuery
                .OrderByDescending(v => v.Date)
                .ThenByDescending(v => v.Id)
                .Skip((currentPage - 1) * normalizedPageSize)
                .Take(normalizedPageSize)
                .ToListAsync();

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

            var model = new PaginatedListViewModel<DisbursementVoucher>
            {
                Items = vouchers,
                TotalCount = totalCount,
                PageIndex = currentPage,
                PageSize = normalizedPageSize,
                SearchTerm = searchTerm,
                FromDate = fromDate,
                ToDate = toDate
            };

            return View(model);
        }

        private IQueryable<DisbursementVoucher> ApplySearchFilters(IQueryable<DisbursementVoucher> query, string term)
        {
            var likeTerm = $"%{term}%";
            var normalized = term.ToLowerInvariant();
            var decimalTerm = TryParseDecimal(term);
            var dateTerm = TryParseDate(term);
            var statusMatches = new List<DisbursementVoucherStatus>();

            if (normalized.Contains("معتمد") || normalized.Contains("approved"))
            {
                statusMatches.Add(DisbursementVoucherStatus.Approved);
            }

            if (normalized.Contains("مرفوض") || normalized.Contains("rejected"))
            {
                statusMatches.Add(DisbursementVoucherStatus.Rejected);
            }

            if (normalized.Contains("بانتظار") || normalized.Contains("pending"))
            {
                statusMatches.Add(DisbursementVoucherStatus.PendingApproval);
            }

            int? voucherId = int.TryParse(term, out var parsedId) ? parsedId : null;

            return query.Where(v =>
                (v.Supplier != null && (EF.Functions.Like(v.Supplier.NameAr, likeTerm) || EF.Functions.Like(v.Supplier.NameEn ?? string.Empty, likeTerm))) ||
                EF.Functions.Like(v.Currency.Code, likeTerm) ||
                EF.Functions.Like(v.Notes ?? string.Empty, likeTerm) ||
                EF.Functions.Like(v.AttachmentFileName ?? string.Empty, likeTerm) ||
                EF.Functions.Like(v.AttachmentFilePath ?? string.Empty, likeTerm) ||
                EF.Functions.Like(v.CreatedBy.FirstName, likeTerm) ||
                EF.Functions.Like(v.CreatedBy.LastName, likeTerm) ||
                (v.CreatedBy.PaymentBranch != null && EF.Functions.Like(v.CreatedBy.PaymentBranch.NameAr, likeTerm)) ||
                (v.ApprovedBy != null && (EF.Functions.Like(v.ApprovedBy.FirstName, likeTerm) || EF.Functions.Like(v.ApprovedBy.LastName, likeTerm))) ||
                (decimalTerm.HasValue && (v.Amount == decimalTerm.Value || v.ExchangeRate == decimalTerm.Value || (v.Amount * v.ExchangeRate) == decimalTerm.Value)) ||
                (dateTerm.HasValue &&
                    (EF.Functions.DateDiffDay(v.Date, dateTerm.Value) == 0 ||
                    (v.ApprovedAt.HasValue && EF.Functions.DateDiffDay(v.ApprovedAt.Value, dateTerm.Value) == 0))) ||
                (voucherId.HasValue && v.Id == voucherId.Value) ||
                statusMatches.Contains(v.Status));
        }

        private static decimal? TryParseDecimal(string term)
        {
            if (decimal.TryParse(term, NumberStyles.Any, CultureInfo.InvariantCulture, out var invariantResult))
            {
                return invariantResult;
            }

            if (decimal.TryParse(term, NumberStyles.Any, CultureInfo.CurrentCulture, out var currentResult))
            {
                return currentResult;
            }

            return null;
        }

        private static DateTime? TryParseDate(string term)
        {
            if (DateTime.TryParse(term, CultureInfo.InvariantCulture, DateTimeStyles.AssumeLocal, out var invariantResult))
            {
                return invariantResult;
            }

            if (DateTime.TryParse(term, CultureInfo.CurrentCulture, DateTimeStyles.AssumeLocal, out var currentResult))
            {
                return currentResult;
            }

            return null;
        }

        [Authorize(Policy = "disbursementvouchers.create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var userBranchIds = await GetUserBranchIdsAsync(user.Id);

            ViewBag.Suppliers = await _context.Suppliers
                .AsNoTracking()
                .FilterByAuthorizationAndBranches(SupplierAuthorization.DisbursementVoucher, userBranchIds)
                .Where(s => s.IsActive && s.AccountId != null && s.Account != null && s.Account.IsActive)
                .OrderBy(s => s.NameAr)
                .Select(s => new { s.Id, s.NameAr, CurrencyId = s.Account!.CurrencyId, CurrencyCode = s.Account.Currency.Code })
                .ToListAsync();
            return View(new DisbursementVoucher { Date = DateTime.Now });
        }

        [HttpPost]
        [Authorize(Policy = "disbursementvouchers.create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(DisbursementVoucher model, IFormFile? attachment)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentAccountId == null || user.PaymentBranchId == null)
                return Challenge();

            var userBranchIds = await GetUserBranchIdsAsync(user.Id);

            var supplier = await _context.Suppliers
                .Include(s => s.Account)
                .Include(s => s.SupplierBranches)
                .FirstOrDefaultAsync(s => s.Id == model.SupplierId);
            if (supplier?.Account == null || !supplier.IsActive || !supplier.Account.IsActive)
                ModelState.AddModelError("SupplierId", "المورد غير موجود");
            else if (!supplier.AuthorizedOperations.HasFlag(SupplierAuthorization.DisbursementVoucher)
                || !supplier.SupplierBranches.Any(sb => userBranchIds.Contains(sb.BranchId)))
                ModelState.AddModelError("SupplierId", "المورد المحدد غير مسموح له بهذه العملية أو غير مرتبط بفرعك");
            else
            {
                model.AccountId = supplier.AccountId!.Value;
                model.CurrencyId = supplier.Account.CurrencyId;
            }

            var paymentAccount = await _context.Accounts.FindAsync(user.PaymentAccountId);
            if (supplier?.Account != null && paymentAccount != null)
            {
                if (!paymentAccount.IsActive)
                {
                    ModelState.AddModelError(string.Empty, "حساب الدفع غير نشط");
                }
                else if (paymentAccount.CurrencyId != supplier.Account.CurrencyId)
                {
                    ModelState.AddModelError("SupplierId", "يجب أن تكون الحسابات بنفس العملة");
                }

                if (paymentAccount.Nature == AccountNature.Debit && model.Amount > paymentAccount.CurrentBalance)
                    ModelState.AddModelError(nameof(model.Amount), "الرصيد المتاح في حساب الدفع لا يكفي لإتمام العملية.");
            }

            ModelState.Remove(nameof(DisbursementVoucher.Account));
            ModelState.Remove(nameof(DisbursementVoucher.CreatedBy));
            ModelState.Remove(nameof(DisbursementVoucher.Supplier));
            ModelState.Remove(nameof(DisbursementVoucher.Currency));

            if (!ModelState.IsValid)
            {
                ViewBag.Suppliers = await _context.Suppliers
                    .AsNoTracking()
                    .FilterByAuthorizationAndBranches(SupplierAuthorization.DisbursementVoucher, userBranchIds)
                    .Where(s => s.AccountId != null && s.Account != null)
                    .OrderBy(s => s.NameAr)
                    .Select(s => new { s.Id, s.NameAr, CurrencyId = s.Account!.CurrencyId, CurrencyCode = s.Account.Currency.Code })
                    .ToListAsync();
                return View(model);
            }

            var attachmentResult = await _attachmentStorageService.SaveAsync(attachment, "disbursement-vouchers");
            if (attachmentResult != null)
            {
                model.AttachmentFilePath = attachmentResult.FilePath;
                model.AttachmentFileName = attachmentResult.FileName;
            }

            var currency = await _context.Currencies.FindAsync(model.CurrencyId);
            if (model.ExchangeRate <= 0)
                model.ExchangeRate = currency?.ExchangeRate ?? 1m;

            model.Date = DateTime.Now;
            model.CreatedById = user.Id;
            var definition = await _workflowService.GetActiveDefinitionAsync(WorkflowDocumentType.DisbursementVoucher, user.PaymentBranchId);
            model.Status = definition != null ? DisbursementVoucherStatus.PendingApproval : DisbursementVoucherStatus.Approved;

            _context.DisbursementVouchers.Add(model);
            await _context.SaveChangesAsync();

            var baseAmount = model.Amount * model.ExchangeRate;

            if (definition != null)
            {
                var instance = await _workflowService.StartWorkflowAsync(
                    definition,
                    WorkflowDocumentType.DisbursementVoucher,
                    model.Id,
                    user.Id,
                    user.PaymentBranchId,
                    model.Amount,
                    baseAmount,
                    model.CurrencyId);

                if (instance != null)
                {
                    model.WorkflowInstanceId = instance.Id;
                    await _context.SaveChangesAsync();
                    TempData["InfoMessage"] = "تم إرسال سند الدفع لاعتمادات الموافقة";
                }
                else
                {
                    await _disbursementVoucherProcessor.FinalizeAsync(model, user.Id);
                    TempData["SuccessMessage"] = "تم إنشاء سند الدفع واعتماده فوراً";
                }
            }
            else
            {
                await _disbursementVoucherProcessor.FinalizeAsync(model, user.Id);
                TempData["SuccessMessage"] = "تم إنشاء سند الدفع واعتماده فوراً";
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "disbursementvouchers.view")]
        public async Task<IActionResult> ExportExcel(DateTime? fromDate = null, DateTime? toDate = null, int? branchId = null, string? searchTerm = null)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == user.Id)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            var vouchersQuery = BuildQuery(user, userBranchIds, fromDate, toDate, branchId);

            if (!string.IsNullOrWhiteSpace(searchTerm))
            {
                vouchersQuery = ApplySearchFilters(vouchersQuery, searchTerm.Trim());
            }

            var vouchers = await vouchersQuery
                .OrderByDescending(v => v.Date)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("DisbursementVouchers");

            worksheet.Cell(1, 1).Value = "التاريخ";
            worksheet.Cell(1, 2).Value = "المورد";
            worksheet.Cell(1, 3).Value = "العملة";
            worksheet.Cell(1, 4).Value = "سعر الصرف";
            worksheet.Cell(1, 5).Value = "المبلغ";
            worksheet.Cell(1, 6).Value = "المبلغ بالعملة الأساسية";
            worksheet.Cell(1, 7).Value = "الحالة";
            worksheet.Cell(1, 8).Value = "الفرع";
            worksheet.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var voucher in vouchers)
            {
                worksheet.Cell(row, 1).Value = voucher.Date;
                worksheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-MM-dd";
                worksheet.Cell(row, 2).Value = voucher.Supplier?.NameAr ?? string.Empty;
                worksheet.Cell(row, 3).Value = voucher.Currency?.Code ?? string.Empty;
                worksheet.Cell(row, 4).Value = voucher.ExchangeRate;
                worksheet.Cell(row, 5).Value = voucher.Amount;
                worksheet.Cell(row, 6).Value = voucher.Amount * voucher.ExchangeRate;
                worksheet.Cell(row, 7).Value = voucher.Status.ToString();
                worksheet.Cell(row, 8).Value = voucher.CreatedBy?.PaymentBranch?.NameAr ?? string.Empty;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"DisbursementVouchers_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpPost]
        [Authorize(Policy = "disbursementvouchers.delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, DateTime? fromDate = null, DateTime? toDate = null, string? searchTerm = null, int? branchId = null, int page = 1, int pageSize = 25)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var voucher = await _context.DisbursementVouchers
                .Include(v => v.CreatedBy)
                    .ThenInclude(u => u.PaymentBranch)
                .Include(v => v.WorkflowInstance)
                    .ThenInclude(i => i!.Actions)
                .FirstOrDefaultAsync(v => v.Id == id);

            if (voucher == null)
                return NotFound();

            var userBranchIds = await _context.UserBranches
                .Where(ub => ub.UserId == user.Id)
                .Select(ub => ub.BranchId)
                .ToListAsync();

            if (!CanAccessVoucher(user, voucher.CreatedBy, userBranchIds))
                return Forbid();

            if (voucher.Status == DisbursementVoucherStatus.Approved)
            {
                var journalEntries = await _context.JournalEntries
                    .Include(j => j.Lines)
                        .ThenInclude(l => l.Account)
                    .Where(j => j.Reference == $"DSBV:{voucher.Id}")
                    .ToListAsync();

                foreach (var entry in journalEntries.Where(e => e.Status == JournalEntryStatus.Posted))
                {
                    foreach (var line in entry.Lines)
                    {
                        var account = line.Account;
                        var netAmount = account.Nature == AccountNature.Debit
                            ? line.DebitAmount - line.CreditAmount
                            : line.CreditAmount - line.DebitAmount;

                        account.CurrentBalance -= netAmount;
                        account.UpdatedAt = DateTime.Now;
                    }
                }

                _context.JournalEntryLines.RemoveRange(journalEntries.SelectMany(j => j.Lines));
                _context.JournalEntries.RemoveRange(journalEntries);
            }

            if (voucher.WorkflowInstance != null)
            {
                _context.WorkflowActions.RemoveRange(voucher.WorkflowInstance.Actions);
                _context.WorkflowInstances.Remove(voucher.WorkflowInstance);
            }

            var attachmentPath = voucher.AttachmentFilePath;

            _context.DisbursementVouchers.Remove(voucher);

            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(attachmentPath))
            {
                _attachmentStorageService.Delete(attachmentPath);
            }

            return RedirectToAction(nameof(Index), new { fromDate, toDate, searchTerm, branchId, page, pageSize });
        }

        private IQueryable<DisbursementVoucher> BuildQuery(User user, List<int> userBranchIds, DateTime? fromDate, DateTime? toDate, int? branchId)
        {
            var vouchersQuery = _context.DisbursementVouchers
                .Include(v => v.Supplier)
                .Include(v => v.Currency)
                .Include(v => v.CreatedBy)
                    .ThenInclude(u => u.PaymentBranch)
                .Include(v => v.ApprovedBy)
                .AsNoTracking();

            if (userBranchIds.Any())
            {
                vouchersQuery = vouchersQuery
                    .Where(v => v.CreatedBy.PaymentBranchId.HasValue && userBranchIds.Contains(v.CreatedBy.PaymentBranchId.Value));
            }
            else if (user.PaymentBranchId.HasValue)
            {
                vouchersQuery = vouchersQuery
                    .Where(v => v.CreatedBy.PaymentBranchId == user.PaymentBranchId);
            }
            else
            {
                vouchersQuery = vouchersQuery
                    .Where(v => v.CreatedById == user.Id);
            }

            if (branchId.HasValue)
            {
                vouchersQuery = vouchersQuery.Where(v =>
                    v.CreatedBy.PaymentBranchId.HasValue && v.CreatedBy.PaymentBranchId.Value == branchId.Value);
            }

            if (fromDate.HasValue)
            {
                var startDate = fromDate.Value.Date;
                vouchersQuery = vouchersQuery.Where(v => v.Date >= startDate);
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.Date.AddDays(1);
                vouchersQuery = vouchersQuery.Where(v => v.Date < endDate);
            }

            return vouchersQuery;
        }

        private static bool CanAccessVoucher(User currentUser, User createdBy, List<int> userBranchIds)
        {
            if (userBranchIds.Any())
            {
                return createdBy.PaymentBranchId.HasValue && userBranchIds.Contains(createdBy.PaymentBranchId.Value);
            }

            if (currentUser.PaymentBranchId.HasValue)
            {
                return createdBy.PaymentBranchId == currentUser.PaymentBranchId;
            }

            return currentUser.Id == createdBy.Id;
        }
    }
}
