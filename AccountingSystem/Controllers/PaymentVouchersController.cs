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
using System;
using System.Collections.Generic;
using System.Globalization;
using System.IO;
using System.Linq;
using AccountingSystem.Extensions;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "paymentvouchers.view")]
    public class PaymentVouchersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWorkflowService _workflowService;
        private readonly IPaymentVoucherProcessor _paymentVoucherProcessor;
        private readonly IAttachmentStorageService _attachmentStorageService;

        public PaymentVouchersController(ApplicationDbContext context, UserManager<User> userManager, IWorkflowService workflowService, IPaymentVoucherProcessor paymentVoucherProcessor, IAttachmentStorageService attachmentStorageService)
        {
            _context = context;
            _userManager = userManager;
            _workflowService = workflowService;
            _paymentVoucherProcessor = paymentVoucherProcessor;
            _attachmentStorageService = attachmentStorageService;
        }

        private async Task PopulatePaymentAccountSelectListAsync()
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "SupplierPaymentsParentAccountId");
            if (setting != null && int.TryParse(setting.Value, out var parentAccountId))
            {
                ViewBag.Accounts = await _context.Accounts
                    .Where(a => a.ParentId == parentAccountId && a.IsActive)
                    .Include(a => a.Currency)
                    .Select(a => new { a.Id, a.Code, a.NameAr, a.CurrencyId, CurrencyCode = a.Currency.Code })
                    .ToListAsync();
            }
            else
            {
                ViewBag.Accounts = new List<object>();
            }
        }

        private async Task<List<int>> GetUserBranchIdsAsync(string userId)
        {
            return await _context.UserBranches
                .Where(ub => ub.UserId == userId)
                .Select(ub => ub.BranchId)
                .ToListAsync();
        }

        private async Task PopulateSupplierSelectListAsync(IReadOnlyCollection<int> userBranchIds)
        {
            ViewBag.Suppliers = await _context.Suppliers
                .AsNoTracking()
                .FilterByAuthorizationAndBranches(SupplierAuthorization.PaymentVoucher, userBranchIds)
                .Where(s => s.IsActive && s.AccountId != null && s.Account != null && s.Account.IsActive)
                .OrderBy(s => s.NameAr)
                .Select(s => new
                {
                    s.Id,
                    s.NameAr,
                    AccountId = s.AccountId!.Value,
                    CurrencyId = s.Account!.CurrencyId,
                    CurrencyCode = s.Account.Currency.Code
                })
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

            var voucherIds = vouchers.Select(v => v.Id).ToList();

            var journalEntryLookup = new Dictionary<int, (int Id, string Number, string Reference)>();

            if (vouchers.Any())
            {
                var referenceMap = vouchers.ToDictionary(
                    v => v.SupplierId.HasValue ? $"سند مصاريف:{v.Id}" : $"سند دفع وكيل:{v.Id}",
                    v => v.Id);

                var references = referenceMap.Keys.ToList();

                var journalEntries = await _context.JournalEntries
                    .Where(j => j.Reference != null && references.Contains(j.Reference))
                    .Select(j => new { j.Id, j.Number, j.Reference })
                    .ToListAsync();

                foreach (var entry in journalEntries)
                {
                    if (entry.Reference != null && referenceMap.TryGetValue(entry.Reference, out var voucherId))
                    {
                        journalEntryLookup[voucherId] = (entry.Id, entry.Number, entry.Reference);
                    }
                }
            }

            var workflowInstances = await _context.WorkflowInstances
                .Where(i => i.DocumentType == WorkflowDocumentType.PaymentVoucher && voucherIds.Contains(i.DocumentId))
                .Include(i => i.Actions)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var workflowLookup = workflowInstances
                .GroupBy(i => i.DocumentId)
                .ToDictionary(g => g.Key, g => g.First());

            var items = vouchers.Select(v =>
            {
                workflowLookup.TryGetValue(v.Id, out var instance);
                var rejectionReason = instance?.Actions?
                    .Where(a => a.Status == WorkflowActionStatus.Rejected)
                    .OrderByDescending(a => a.ActionedAt)
                    .Select(a => a.Notes)
                    .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n));

                if (journalEntryLookup.TryGetValue(v.Id, out var entry))
                {
                    return new PaymentVoucherListItemViewModel
                    {
                        Voucher = v,
                        JournalEntryId = entry.Id,
                        JournalEntryNumber = entry.Number,
                        JournalEntryReference = entry.Reference,
                        RejectionReason = rejectionReason
                    };
                }

                return new PaymentVoucherListItemViewModel
                {
                    Voucher = v,
                    RejectionReason = rejectionReason
                };
            }).ToList();

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

            var model = new PaginatedListViewModel<PaymentVoucherListItemViewModel>
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

        private IQueryable<PaymentVoucher> ApplySearchFilters(IQueryable<PaymentVoucher> query, string term)
        {
            var likeTerm = $"%{term}%";
            var normalized = term.ToLowerInvariant();
            var decimalTerm = TryParseDecimal(term);
            var dateTerm = TryParseDate(term);
            var statusMatches = new List<PaymentVoucherStatus>();

            if (normalized.Contains("مسودة") || normalized.Contains("draft"))
            {
                statusMatches.Add(PaymentVoucherStatus.Draft);
            }

            if (normalized.Contains("بانتظار") || normalized.Contains("pending"))
            {
                statusMatches.Add(PaymentVoucherStatus.PendingApproval);
            }

            if (normalized.Contains("معتمد") || normalized.Contains("approved"))
            {
                statusMatches.Add(PaymentVoucherStatus.Approved);
            }

            if (normalized.Contains("مرفوض") || normalized.Contains("rejected"))
            {
                statusMatches.Add(PaymentVoucherStatus.Rejected);
            }

            int? voucherId = int.TryParse(term, out var parsedId) ? parsedId : null;

            return query.Where(v =>
                (v.Supplier != null && (EF.Functions.Like(v.Supplier.NameAr, likeTerm) || EF.Functions.Like(v.Supplier.NameEn ?? string.Empty, likeTerm))) ||
                (v.Agent != null && EF.Functions.Like(v.Agent.Name, likeTerm)) ||
                (v.Account != null && (EF.Functions.Like(v.Account.NameAr, likeTerm) || EF.Functions.Like(v.Account.Code, likeTerm))) ||
                EF.Functions.Like(v.Currency.Code, likeTerm) ||
                EF.Functions.Like(v.Notes ?? string.Empty, likeTerm) ||
                EF.Functions.Like(v.AttachmentFileName ?? string.Empty, likeTerm) ||
                EF.Functions.Like(v.AttachmentFilePath ?? string.Empty, likeTerm) ||
                EF.Functions.Like(v.CreatedBy.FirstName, likeTerm) ||
                EF.Functions.Like(v.CreatedBy.LastName, likeTerm) ||
                (v.CreatedBy.PaymentBranch != null && EF.Functions.Like(v.CreatedBy.PaymentBranch.NameAr, likeTerm)) ||
                (v.ApprovedBy != null && (EF.Functions.Like(v.ApprovedBy.FirstName, likeTerm) || EF.Functions.Like(v.ApprovedBy.LastName, likeTerm))) ||
                (decimalTerm.HasValue && (v.Amount == decimalTerm.Value || v.ExchangeRate == decimalTerm.Value)) ||
                (dateTerm.HasValue &&
                    (EF.Functions.DateDiffDay(v.Date, dateTerm.Value) == 0 ||
                    (v.ApprovedAt.HasValue && EF.Functions.DateDiffDay(v.ApprovedAt.Value, dateTerm.Value) == 0))) ||
                (voucherId.HasValue && v.Id == voucherId.Value) ||
                statusMatches.Contains(v.Status) ||
                _context.JournalEntries.Any(j =>
                    j.Reference != null &&
                    (j.Reference == "سند مصاريف:" + v.Id.ToString() ||
                     j.Reference == "سند دفع وكيل:" + v.Id.ToString()) &&
                    (EF.Functions.Like(j.Number, likeTerm) || EF.Functions.Like(j.Reference, likeTerm))));
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

        [Authorize(Policy = "paymentvouchers.create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var userBranchIds = await GetUserBranchIdsAsync(user.Id);

            await PopulateSupplierSelectListAsync(userBranchIds);
            await PopulatePaymentAccountSelectListAsync();

            return View(new PaymentVoucher { Date = DateTime.Now, IsCash = true });
        }

        [HttpPost]
        [Authorize(Policy = "paymentvouchers.create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Create(PaymentVoucher model, IFormFile? attachment)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentAccountId == null || user.PaymentBranchId == null)
                return Challenge();

            model.Date = DateTime.Now;

            var userBranchIds = await GetUserBranchIdsAsync(user.Id);

            if (!model.SupplierId.HasValue)
            {
                ModelState.AddModelError(nameof(PaymentVoucher.SupplierId), "الرجاء اختيار المورد");
            }

            Supplier? supplier = null;
            if (model.SupplierId.HasValue)
            {
                supplier = await _context.Suppliers
                    .Include(s => s.Account)
                    .Include(s => s.SupplierBranches)
                    .FirstOrDefaultAsync(s => s.Id == model.SupplierId.Value);
                if (supplier?.Account == null || !supplier.IsActive || !supplier.Account.IsActive)
                {
                    ModelState.AddModelError(nameof(PaymentVoucher.SupplierId), "المورد غير موجود");
                }
                else if (!supplier.AuthorizedOperations.HasFlag(SupplierAuthorization.PaymentVoucher)
                    || !supplier.SupplierBranches.Any(sb => userBranchIds.Contains(sb.BranchId)))
                {
                    ModelState.AddModelError(nameof(PaymentVoucher.SupplierId), "المورد المحدد غير مسموح له بهذه العملية أو غير مرتبط بفرعك");
                }
            }

            Account? selectedAccount = await _context.Accounts.FindAsync(model.AccountId);
            var settingAccount = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "SupplierPaymentsParentAccountId");
            if (selectedAccount == null || !selectedAccount.IsActive || settingAccount == null || !int.TryParse(settingAccount.Value, out var parentId) || selectedAccount.ParentId != parentId)
                ModelState.AddModelError("AccountId", "الحساب غير صالح");

            Account? cashAccount = null;
            if (model.IsCash)
            {
                cashAccount = await _context.Accounts.FindAsync(user.PaymentAccountId.Value);
                if (cashAccount != null && cashAccount.Nature == AccountNature.Debit && model.Amount > cashAccount.CurrentBalance)
                    ModelState.AddModelError(nameof(model.Amount), "الرصيد المتاح في حساب الدفع لا يكفي لإتمام العملية.");
            }

            if (supplier?.Account != null && selectedAccount != null)
            {
                if (supplier.Account.CurrencyId != selectedAccount.CurrencyId)
                    ModelState.AddModelError(nameof(PaymentVoucher.SupplierId), "يجب أن تكون الحسابات بنفس العملة");
                if (model.IsCash && cashAccount != null && selectedAccount.CurrencyId != cashAccount.CurrencyId)
                    ModelState.AddModelError("AccountId", "يجب أن تكون الحسابات بنفس العملة");
            }

            if (supplier?.Account != null)
            {
                model.CurrencyId = supplier.Account.CurrencyId;
            }

            ModelState.Remove(nameof(PaymentVoucher.CurrencyId));
            ModelState.Remove(nameof(PaymentVoucher.ExchangeRate));

            if (!ModelState.IsValid)
            {
                await PopulateSupplierSelectListAsync(userBranchIds);
                await PopulatePaymentAccountSelectListAsync();
                return View(model);
            }

            await AttachFileIfPresentAsync(model, attachment, "payment-vouchers");

            return await FinalizeCreationAsync(model, user);
        }
        [Authorize(Policy = "paymentvouchers.create")]
        public async Task<IActionResult> CreateFromAgent()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            if (user.AgentId == null)
            {
                TempData["ErrorMessage"] = "لم يتم ربط المستخدم بأي وكيل.";
                return RedirectToAction(nameof(Index));
            }

            if (user.PaymentAccountId == null || user.PaymentBranchId == null)
            {
                TempData["ErrorMessage"] = "لم يتم إعداد حساب الدفع للمستخدم.";
                return RedirectToAction(nameof(Index));
            }

            var agent = await _context.Agents
                .Include(a => a.Account).ThenInclude(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == user.AgentId.Value);

            if (agent?.Account == null)
            {
                TempData["ErrorMessage"] = "لا يملك الوكيل المحدد حساباً مالياً.";
                return RedirectToAction(nameof(Index));
            }

            var paymentAccount = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == user.PaymentAccountId.Value);

            if (paymentAccount == null)
            {
                TempData["ErrorMessage"] = "حساب الدفع المحدد للمستخدم غير موجود.";
                return RedirectToAction(nameof(Index));
            }

            if (paymentAccount.CurrencyId != agent.Account.CurrencyId)
            {
                TempData["ErrorMessage"] = "عملة حساب الدفع لا تطابق عملة حساب الوكيل.";
                return RedirectToAction(nameof(Index));
            }

            ViewBag.Agent = new { agent.Id, agent.Name };
            ViewBag.PaymentAccount = new
            {
                paymentAccount.Id,
                paymentAccount.Code,
                paymentAccount.NameAr
            };
            ViewBag.CurrencyCode = agent.Account.Currency.Code;

            var model = new PaymentVoucher
            {
                Date = DateTime.Now,
                AgentId = agent.Id,
                AccountId = paymentAccount.Id,
                CurrencyId = agent.Account.CurrencyId,
                IsCash = false
            };

            return View(model);
        }

        [HttpPost]
        [Authorize(Policy = "paymentvouchers.create")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> CreateFromAgent(PaymentVoucher model, IFormFile? attachment)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null || user.PaymentAccountId == null || user.PaymentBranchId == null)
                return Challenge();

            model.Date = DateTime.Now;

            ModelState.Remove(nameof(PaymentVoucher.SupplierId));
            ModelState.Remove(nameof(PaymentVoucher.AccountId));
            ModelState.Remove(nameof(PaymentVoucher.AgentId));
            ModelState.Remove(nameof(PaymentVoucher.CurrencyId));
            ModelState.Remove(nameof(PaymentVoucher.ExchangeRate));

            if (user.AgentId == null)
            {
                ModelState.AddModelError(string.Empty, "لم يتم ربط المستخدم بأي وكيل.");
            }

            var agent = user.AgentId.HasValue
                ? await _context.Agents
                    .Include(a => a.Account).ThenInclude(a => a.Currency)
                    .FirstOrDefaultAsync(a => a.Id == user.AgentId.Value)
                : null;

            if (agent?.Account == null)
            {
                ModelState.AddModelError(nameof(PaymentVoucher.AgentId), "لا يملك الوكيل المحدد حساباً مالياً.");
            }

            var paymentAccount = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == user.PaymentAccountId.Value);

            if (paymentAccount == null)
            {
                ModelState.AddModelError(nameof(PaymentVoucher.AccountId), "حساب الدفع المحدد للمستخدم غير موجود.");
            }

            if (agent?.Account != null && paymentAccount != null && agent.Account.CurrencyId != paymentAccount.CurrencyId)
            {
                ModelState.AddModelError(nameof(PaymentVoucher.AgentId), "عملة حساب الوكيل لا تطابق عملة حساب الدفع.");
            }

            if (paymentAccount != null && paymentAccount.Nature == AccountNature.Debit && model.Amount > paymentAccount.CurrentBalance)
            {
                ModelState.AddModelError(nameof(PaymentVoucher.Amount), "الرصيد المتاح في حساب الدفع لا يكفي لإتمام العملية.");
            }

            if (agent?.Account != null && paymentAccount != null)
            {
                model.AgentId = agent.Id;
                model.AccountId = paymentAccount.Id;
                model.CurrencyId = agent.Account.CurrencyId;
                model.SupplierId = null;
                model.IsCash = false;
            }

            if (!ModelState.IsValid)
            {
                ViewBag.Agent = agent != null ? new { agent.Id, agent.Name } : null;
                ViewBag.PaymentAccount = paymentAccount != null ? new { paymentAccount.Id, paymentAccount.Code, paymentAccount.NameAr } : null;
                ViewBag.CurrencyCode = agent?.Account?.Currency?.Code;
                return View(model);
            }
            model.Notes = "سند صرف وكيل " + Environment.NewLine + model.Notes;
            await AttachFileIfPresentAsync(model, attachment, "payment-vouchers");

            return await FinalizeCreationAsync(model, user);
        }

        [Authorize(Policy = "paymentvouchers.view")]
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
            var worksheet = workbook.Worksheets.Add("PaymentVouchers");

            worksheet.Cell(1, 1).Value = "التاريخ";
            worksheet.Cell(1, 2).Value = "المورد";
            worksheet.Cell(1, 3).Value = "الوكيل";
            worksheet.Cell(1, 4).Value = "العملة";
            worksheet.Cell(1, 5).Value = "سعر الصرف";
            worksheet.Cell(1, 6).Value = "المبلغ";
            worksheet.Cell(1, 7).Value = "الحالة";
            worksheet.Cell(1, 8).Value = "الفرع";
            worksheet.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var voucher in vouchers)
            {
                worksheet.Cell(row, 1).Value = voucher.Date;
                worksheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-MM-dd";
                worksheet.Cell(row, 2).Value = voucher.Supplier?.NameAr ?? string.Empty;
                worksheet.Cell(row, 3).Value = voucher.Agent?.Name ?? string.Empty;
                worksheet.Cell(row, 4).Value = voucher.Currency?.Code ?? string.Empty;
                worksheet.Cell(row, 5).Value = voucher.ExchangeRate;
                worksheet.Cell(row, 6).Value = voucher.Amount;
                worksheet.Cell(row, 7).Value = voucher.Status.ToString();
                worksheet.Cell(row, 8).Value = voucher.CreatedBy?.PaymentBranch?.NameAr ?? string.Empty;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"PaymentVouchers_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(),
                "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet",
                fileName);
        }

        [HttpPost]
        [Authorize(Policy = "paymentvouchers.delete")]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> Delete(int id, DateTime? fromDate = null, DateTime? toDate = null, string? searchTerm = null, int? branchId = null, int page = 1, int pageSize = 25)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var voucher = await _context.PaymentVouchers
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

            var journalReferences = new[]
            {
                $"سند مصاريف:{voucher.Id}",
                $"سند دفع وكيل:{voucher.Id}"
            };

            var journalEntries = await _context.JournalEntries
                .Include(j => j.Lines)
                    .ThenInclude(l => l.Account)
                .Where(j => journalReferences.Contains(j.Reference!))
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

            if (voucher.WorkflowInstance != null)
            {
                _context.WorkflowActions.RemoveRange(voucher.WorkflowInstance.Actions);
                _context.WorkflowInstances.Remove(voucher.WorkflowInstance);
            }

            _context.JournalEntryLines.RemoveRange(journalEntries.SelectMany(j => j.Lines));
            _context.JournalEntries.RemoveRange(journalEntries);
            var attachmentPath = voucher.AttachmentFilePath;

            _context.PaymentVouchers.Remove(voucher);

            await _context.SaveChangesAsync();

            if (!string.IsNullOrEmpty(attachmentPath))
            {
                _attachmentStorageService.Delete(attachmentPath);
            }

            return RedirectToAction(nameof(Index), new { fromDate, toDate, searchTerm, branchId, page, pageSize });
        }

        private async Task<IActionResult> FinalizeCreationAsync(PaymentVoucher model, User user)
        {
            var currency = await _context.Currencies.FindAsync(model.CurrencyId);
            model.ExchangeRate = currency?.ExchangeRate ?? 1m;

            model.CreatedById = user.Id;
            var definition = await _workflowService.GetActiveDefinitionAsync(WorkflowDocumentType.PaymentVoucher, user.PaymentBranchId);
            model.Status = definition != null ? PaymentVoucherStatus.PendingApproval : PaymentVoucherStatus.Approved;

            _context.PaymentVouchers.Add(model);
            await _context.SaveChangesAsync();

            if (definition != null)
            {
                var baseAmount = model.Amount * model.ExchangeRate;
                var instance = await _workflowService.StartWorkflowAsync(
                    definition,
                    WorkflowDocumentType.PaymentVoucher,
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
                }

                TempData["InfoMessage"] = "تم إرسال سند الدفع لاعتمادات الموافقة";
            }
            else
            {
                await _paymentVoucherProcessor.FinalizeVoucherAsync(model, user.Id);
                TempData["SuccessMessage"] = "تم إنشاء سند الدفع واعتماده فوراً";
            }

            return RedirectToAction(nameof(Index));
        }

        private async Task AttachFileIfPresentAsync(PaymentVoucher voucher, IFormFile? attachment, string category)
        {
            if (attachment == null)
            {
                return;
            }

            var result = await _attachmentStorageService.SaveAsync(attachment, category, voucher.AttachmentFilePath);
            if (result != null)
            {
                voucher.AttachmentFilePath = result.FilePath;
                voucher.AttachmentFileName = result.FileName;
            }
        }

        private IQueryable<PaymentVoucher> BuildQuery(User user, List<int> userBranchIds, DateTime? fromDate, DateTime? toDate, int? branchId)
        {
            var vouchersQuery = _context.PaymentVouchers
                .Include(v => v.Supplier).ThenInclude(s => s.Account)
                .Include(v => v.Agent).ThenInclude(a => a.Account)
                .Include(v => v.Account)
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
