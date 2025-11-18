using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Identity;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Models.Workflows;
using AccountingSystem.ViewModels;
using ClosedXML.Excel;
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AccountingSystem.Extensions;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "receiptvouchers.view")]
    public class ReceiptVouchersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWorkflowService _workflowService;
        private readonly IReceiptVoucherProcessor _receiptVoucherProcessor;
        private readonly IAttachmentStorageService _attachmentStorageService;

        public ReceiptVouchersController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IWorkflowService workflowService,
            IReceiptVoucherProcessor receiptVoucherProcessor,
            IAttachmentStorageService attachmentStorageService)
        {
            _context = context;
            _userManager = userManager;
            _workflowService = workflowService;
            _receiptVoucherProcessor = receiptVoucherProcessor;
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

            var voucherReferences = vouchers
                .Select(v => $"RCV:{v.Id}")
                .ToList();

            var journalEntryLookup = new Dictionary<int, (int Id, string Number, string Reference)>();

            if (voucherReferences.Count > 0)
            {
                var journalEntries = await _context.JournalEntries
                    .AsNoTracking()
                    .Where(j => j.Reference != null && voucherReferences.Contains(j.Reference))
                    .Select(j => new { j.Id, j.Reference, j.Number })
                    .ToListAsync();

                foreach (var entry in journalEntries)
                {
                    if (entry.Reference != null && entry.Reference.StartsWith("RCV:", StringComparison.OrdinalIgnoreCase)
                        && int.TryParse(entry.Reference[4..], out var voucherId))
                    {
                        journalEntryLookup[voucherId] = (entry.Id, entry.Number, entry.Reference);
                    }
                }
            }

            var items = vouchers
                .Select(v =>
                {
                    if (journalEntryLookup.TryGetValue(v.Id, out var entry))
                    {
                        return new ReceiptVoucherListItemViewModel
                        {
                            Voucher = v,
                            JournalEntryId = entry.Id,
                            JournalEntryNumber = entry.Number,
                            JournalEntryReference = entry.Reference
                        };
                    }

                    return new ReceiptVoucherListItemViewModel
                    {
                        Voucher = v
                    };
                })
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

            var model = new PaginatedListViewModel<ReceiptVoucherListItemViewModel>
            {
                Items = items,
                TotalCount = totalCount,
                PageIndex = currentPage,
                PageSize = normalizedPageSize,
                SearchTerm = searchTerm,
                FromDate = fromDate,
                ToDate = toDate
            };

            return View(model);
        }

        private IQueryable<ReceiptVoucher> ApplySearchFilters(IQueryable<ReceiptVoucher> query, string term)
        {
            var likeTerm = $"%{term}%";
            var normalized = term.ToLowerInvariant();
            var decimalTerm = TryParseDecimal(term);
            var dateTerm = TryParseDate(term);
            var statusMatches = new List<ReceiptVoucherStatus>();

            if (normalized.Contains("معتمد") || normalized.Contains("approved"))
            {
                statusMatches.Add(ReceiptVoucherStatus.Approved);
            }

            if (normalized.Contains("مرفوض") || normalized.Contains("rejected"))
            {
                statusMatches.Add(ReceiptVoucherStatus.Rejected);
            }

            if (normalized.Contains("بانتظار") || normalized.Contains("انتظار") || normalized.Contains("pending"))
            {
                statusMatches.Add(ReceiptVoucherStatus.PendingApproval);
            }

            int? voucherId = int.TryParse(term, out var parsedId) ? parsedId : null;

            return query.Where(v =>
                (v.Supplier != null && (EF.Functions.Like(v.Supplier.NameAr, likeTerm) || EF.Functions.Like(v.Supplier.NameEn ?? string.Empty, likeTerm))) ||
                EF.Functions.Like(v.PaymentAccount.NameAr, likeTerm) ||
                EF.Functions.Like(v.Account.NameAr, likeTerm) ||
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
                statusMatches.Contains(v.Status) ||
                _context.JournalEntries.Any(j =>
                    j.Reference != null &&
                    j.Reference == string.Concat("RCV:", v.Id) &&
                    (EF.Functions.Like(j.Number, likeTerm) || EF.Functions.Like(j.Reference, likeTerm))));
        }

        [Authorize(Policy = "receiptvouchers.create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            if (user.PaymentBranchId == null)
            {
                TempData["ErrorMessage"] = "لم يتم إعداد فرع الصندوق للمستخدم.";
                return RedirectToAction(nameof(Index));
            }

            var paymentAccounts = await _context.UserPaymentAccounts
                .AsNoTracking()
                .Where(u => u.UserId == user.Id && u.Account.IsActive)
                .Include(u => u.Account).ThenInclude(a => a.Currency)
                .Select(u => new { u.Account.Id, u.Account.Code, u.Account.NameAr, u.Account.CurrencyId, CurrencyCode = u.Account.Currency.Code })
                .ToListAsync();
            if (!paymentAccounts.Any())
            {
                TempData["ErrorMessage"] = "لم يتم ربط أي حسابات دفع بالمستخدم.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.PaymentAccounts = paymentAccounts;

            var userBranchIds = await GetUserBranchIdsAsync(user.Id);

            var suppliers = await _context.Suppliers
                .AsNoTracking()
                .FilterByAuthorizationAndBranches(SupplierAuthorization.ReceiptVoucher, userBranchIds)
                .Where(s => s.IsActive && s.AccountId != null && s.Account != null && s.Account.IsActive)
                .OrderBy(s => s.NameAr)
                .Select(s => new
                {
                    s.Id,
                    s.NameAr,
                    AccountId = s.AccountId!.Value,
                    s.Account!.CurrencyId,
                    CurrencyCode = s.Account.Currency.Code
                })
                .ToListAsync();
            if (!suppliers.Any())
            {
                TempData["ErrorMessage"] = "لا يوجد موردون مرتبطون بحسابات مالية.";
                return RedirectToAction(nameof(Index));
            }
            ViewBag.Suppliers = suppliers;
            var model = new ReceiptVoucher { Date = DateTime.Now };
            var defaultPaymentAccount = user?.PaymentAccountId;
            if (defaultPaymentAccount.HasValue && paymentAccounts.Any(a => a.Id == defaultPaymentAccount.Value))
            {
                model.PaymentAccountId = defaultPaymentAccount.Value;
            }

            return View(model);
        }

        [HttpPost]
        [Authorize(Policy = "receiptvouchers.create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(ReceiptVoucher model, IFormFile? attachment)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            if (user.PaymentBranchId == null)
            {
                TempData["ErrorMessage"] = "لم يتم إعداد فرع الصندوق للمستخدم.";
                return RedirectToAction(nameof(Index));
            }

            var userBranchIds = await GetUserBranchIdsAsync(user.Id);

            ModelState.Remove(nameof(ReceiptVoucher.Account));
            ModelState.Remove(nameof(ReceiptVoucher.Currency));
            ModelState.Remove(nameof(ReceiptVoucher.CreatedBy));
            ModelState.Remove(nameof(ReceiptVoucher.Supplier));
            ModelState.Remove(nameof(ReceiptVoucher.PaymentAccount));

            Account? account = null;
            Account? paymentAccount = null;

            if (!model.SupplierId.HasValue)
            {
                ModelState.AddModelError(nameof(ReceiptVoucher.SupplierId), "الرجاء اختيار المورد");
            }

            if (model.SupplierId.HasValue)
            {
                var supplier = await _context.Suppliers
                    .Include(s => s.Account)
                    .ThenInclude(a => a.Currency)
                    .Include(s => s.SupplierBranches)
                    .FirstOrDefaultAsync(s => s.Id == model.SupplierId.Value);

                if (supplier?.Account == null || !supplier.IsActive || !supplier.Account.IsActive)
                {
                    ModelState.AddModelError(nameof(ReceiptVoucher.SupplierId), "المورد غير موجود أو لا يملك حساباً");
                }
                else if (!supplier.AuthorizedOperations.HasFlag(SupplierAuthorization.ReceiptVoucher)
                    || !supplier.SupplierBranches.Any(sb => userBranchIds.Contains(sb.BranchId)))
                {
                    ModelState.AddModelError(nameof(ReceiptVoucher.SupplierId), "المورد المحدد غير مسموح له بهذه العملية أو غير مرتبط بفرعك");
                }
                else
                {
                    account = supplier.Account;
                    model.AccountId = supplier.AccountId!.Value;
                    model.CurrencyId = supplier.Account.CurrencyId;
                    ModelState.Remove(nameof(ReceiptVoucher.AccountId));
                    ModelState.Remove(nameof(ReceiptVoucher.CurrencyId));
                }
            }

            if (model.PaymentAccountId != 0)
            {
                var allowedPaymentAccount = await _context.UserPaymentAccounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(u => u.UserId == user.Id && u.AccountId == model.PaymentAccountId);

                if (allowedPaymentAccount == null)
                {
                    ModelState.AddModelError(nameof(ReceiptVoucher.PaymentAccountId), "حساب الدفع المحدد غير متاح للمستخدم");
                }
                else
                {
                    paymentAccount = await _context.Accounts.FindAsync(model.PaymentAccountId);
                    if (paymentAccount == null)
                    {
                        ModelState.AddModelError(nameof(ReceiptVoucher.PaymentAccountId), "حساب الدفع غير موجود");
                    }
                    else if (!paymentAccount.IsActive)
                    {
                        ModelState.AddModelError(nameof(ReceiptVoucher.PaymentAccountId), "حساب الدفع غير نشط");
                    }
                }
            }
            else
            {
                ModelState.AddModelError(nameof(ReceiptVoucher.PaymentAccountId), "الرجاء اختيار حساب الدفع");
            }

            if (account != null && paymentAccount != null && paymentAccount.CurrencyId != account.CurrencyId)
            {
                ModelState.AddModelError(nameof(ReceiptVoucher.PaymentAccountId), "يجب أن تكون الحسابات بنفس العملة");
            }
            ModelState.Remove(nameof(ReceiptVoucher.CreatedById));

            if (!ModelState.IsValid)
            {
                var paymentAccounts = await _context.UserPaymentAccounts
                    .AsNoTracking()
                    .Where(u => u.UserId == user.Id)
                    .Include(u => u.Account).ThenInclude(a => a.Currency)
                    .Select(u => new { u.Account.Id, u.Account.Code, u.Account.NameAr, u.Account.CurrencyId, CurrencyCode = u.Account.Currency.Code })
                    .ToListAsync();
                if (!paymentAccounts.Any())
                {
                    TempData["ErrorMessage"] = "لم يتم ربط أي حسابات دفع بالمستخدم.";
                    return RedirectToAction(nameof(Index));
                }
                ViewBag.PaymentAccounts = paymentAccounts;

                var suppliers = await _context.Suppliers
                    .AsNoTracking()
                    .FilterByAuthorizationAndBranches(SupplierAuthorization.ReceiptVoucher, userBranchIds)
                    .Where(s => s.AccountId != null && s.Account != null)
                    .OrderBy(s => s.NameAr)
                    .Select(s => new
                    {
                        s.Id,
                        s.NameAr,
                        AccountId = s.AccountId!.Value,
                        s.Account!.CurrencyId,
                        CurrencyCode = s.Account.Currency.Code
                    })
                    .ToListAsync();
                if (!suppliers.Any())
                {
                    TempData["ErrorMessage"] = "لا يوجد موردون مرتبطون بحسابات مالية.";
                    return RedirectToAction(nameof(Index));
                }
                ViewBag.Suppliers = suppliers;
                return View(model);
            }

            var attachmentResult = await _attachmentStorageService.SaveAsync(attachment, "receipt-vouchers");
            if (attachmentResult != null)
            {
                model.AttachmentFilePath = attachmentResult.FilePath;
                model.AttachmentFileName = attachmentResult.FileName;
            }

            var currency = await _context.Currencies.FindAsync(model.CurrencyId);
            if (model.ExchangeRate <= 0)
                model.ExchangeRate = currency?.ExchangeRate ?? 1m;

            model.CreatedById = user.Id;
            var definition = await _workflowService.GetActiveDefinitionAsync(WorkflowDocumentType.ReceiptVoucher, user.PaymentBranchId);
            model.Status = definition != null ? ReceiptVoucherStatus.PendingApproval : ReceiptVoucherStatus.Approved;

            _context.ReceiptVouchers.Add(model);
            await _context.SaveChangesAsync();

            var baseAmount = model.Amount * model.ExchangeRate;

            if (definition != null)
            {
                var instance = await _workflowService.StartWorkflowAsync(
                    definition,
                    WorkflowDocumentType.ReceiptVoucher,
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
                    TempData["InfoMessage"] = "تم إرسال سند القبض لاعتمادات الموافقة";
                }
                else
                {
                    await _receiptVoucherProcessor.FinalizeAsync(model, user.Id);
                    TempData["SuccessMessage"] = "تم إنشاء سند القبض واعتماده فوراً";
                }
            }
            else
            {
                await _receiptVoucherProcessor.FinalizeAsync(model, user.Id);
                TempData["SuccessMessage"] = "تم إنشاء سند القبض واعتماده فوراً";
            }

            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "receiptvouchers.view")]
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
            var worksheet = workbook.Worksheets.Add("ReceiptVouchers");

            worksheet.Cell(1, 1).Value = "التاريخ";
            worksheet.Cell(1, 2).Value = "المورد";
            worksheet.Cell(1, 3).Value = "حساب الدفع";
            worksheet.Cell(1, 4).Value = "حساب المورد";
            worksheet.Cell(1, 5).Value = "العملة";
            worksheet.Cell(1, 6).Value = "سعر الصرف";
            worksheet.Cell(1, 7).Value = "المبلغ";
            worksheet.Cell(1, 8).Value = "المبلغ بالعملة الأساسية";
            worksheet.Cell(1, 9).Value = "الحالة";
            worksheet.Cell(1, 10).Value = "الفرع";
            worksheet.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var voucher in vouchers)
            {
                worksheet.Cell(row, 1).Value = voucher.Date;
                worksheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-MM-dd";
                worksheet.Cell(row, 2).Value = voucher.Supplier?.NameAr ?? string.Empty;
                worksheet.Cell(row, 3).Value = voucher.PaymentAccount?.NameAr ?? string.Empty;
                worksheet.Cell(row, 4).Value = voucher.Account?.NameAr ?? string.Empty;
                worksheet.Cell(row, 5).Value = voucher.Currency?.Code ?? string.Empty;
                worksheet.Cell(row, 6).Value = voucher.ExchangeRate;
                worksheet.Cell(row, 7).Value = voucher.Amount;
                worksheet.Cell(row, 8).Value = voucher.Amount * voucher.ExchangeRate;
                worksheet.Cell(row, 9).Value = voucher.Status.ToString();
                worksheet.Cell(row, 10).Value = voucher.CreatedBy?.PaymentBranch?.NameAr ?? string.Empty;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"ReceiptVouchers_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpPost]
        [Authorize(Policy = "receiptvouchers.delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, DateTime? fromDate = null, DateTime? toDate = null, string? searchTerm = null, int? branchId = null, int page = 1, int pageSize = 25)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var voucher = await _context.ReceiptVouchers
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

            if (voucher.Status == ReceiptVoucherStatus.Approved)
            {
                var journalEntries = await _context.JournalEntries
                    .Include(j => j.Lines)
                        .ThenInclude(l => l.Account)
                    .Where(j => j.Reference == $"RCV:{voucher.Id}")
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

            _context.ReceiptVouchers.Remove(voucher);

            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(attachmentPath))
            {
                _attachmentStorageService.Delete(attachmentPath);
            }

            return RedirectToAction(nameof(Index), new { fromDate, toDate, searchTerm, branchId, page, pageSize });
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

        private IQueryable<ReceiptVoucher> BuildQuery(User user, List<int> userBranchIds, DateTime? fromDate, DateTime? toDate, int? branchId)
        {
            var vouchersQuery = _context.ReceiptVouchers
                .Include(v => v.Account)
                .Include(v => v.PaymentAccount)
                .Include(v => v.Currency)
                .Include(v => v.Supplier)
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
