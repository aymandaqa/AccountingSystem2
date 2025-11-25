using System;
using System.Collections.Generic;
using System.Linq;
using AccountingSystem.Data;
using AccountingSystem.Extensions;
using AccountingSystem.Models;
using AccountingSystem.Services;
using AccountingSystem.Models.Workflows;
using AccountingSystem.ViewModels;
using ClosedXML.Excel;
using System.IO;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "assetexpenses.view")]
    public class AssetExpensesController : Controller
    {
        private const string AssetExpenseApprovedMessage = "تم إنشاء مصروف الأصل واعتماده فوراً";

        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IWorkflowService _workflowService;
        private readonly IAssetExpenseProcessor _assetExpenseProcessor;
        private readonly IAttachmentStorageService _attachmentStorageService;

        public AssetExpensesController(
            ApplicationDbContext context,
            UserManager<User> userManager,
            IWorkflowService workflowService,
            IAssetExpenseProcessor assetExpenseProcessor,
            IAttachmentStorageService attachmentStorageService)
        {
            _context = context;
            _userManager = userManager;
            _workflowService = workflowService;
            _assetExpenseProcessor = assetExpenseProcessor;
            _attachmentStorageService = attachmentStorageService;
        }

        private async Task<List<int>> GetUserBranchIdsAsync(string userId)
        {
            return await _context.UserBranches
                .Where(ub => ub.UserId == userId)
                .Select(ub => ub.BranchId)
                .ToListAsync();
        }

        public async Task<IActionResult> Index(string? searchTerm, bool showMyExpenses = false, int page = 1, int pageSize = 10)
        {
            const int maxPageSize = 100;
            if (page < 1)
            {
                page = 1;
            }

            if (pageSize < 1)
            {
                pageSize = 10;
            }
            else if (pageSize > maxPageSize)
            {
                pageSize = maxPageSize;
            }

            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var userBranchIds = await GetUserBranchIdsAsync(user.Id);

            var query = _context.AssetExpenses
                .AsNoTracking()
                .Include(e => e.Asset).ThenInclude(a => a.Branch)
                .Include(e => e.ExpenseAccount)
                .Include(e => e.Supplier)
                .Include(e => e.CreatedBy)
                .AsQueryable();

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

            var trimmedSearch = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();

            if (showMyExpenses)
            {
                var currentUserId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Challenge();
                }

                query = query.Where(e => e.CreatedById == currentUserId);
            }

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                var likeFilter = $"%{trimmedSearch}%";
                query = query.Where(e =>
                    EF.Functions.Like(e.Asset.Name, likeFilter) ||
                    EF.Functions.Like(e.Asset.Branch.NameAr, likeFilter) ||
                    EF.Functions.Like(e.ExpenseAccount.NameAr, likeFilter) ||
                    EF.Functions.Like(e.Supplier.NameAr, likeFilter) ||
                    (e.CreatedBy != null && (
                        (!string.IsNullOrWhiteSpace(e.CreatedBy.FullName) && EF.Functions.Like(e.CreatedBy.FullName!, likeFilter)) ||
                        (!string.IsNullOrWhiteSpace(e.CreatedBy.UserName) && EF.Functions.Like(e.CreatedBy.UserName!, likeFilter))
                    )) ||
                    (!string.IsNullOrWhiteSpace(e.Notes) && EF.Functions.Like(e.Notes!, likeFilter)));
            }

            var totalItems = await query.CountAsync();
            var totalPages = (int)Math.Ceiling(totalItems / (double)pageSize);
            if (totalPages > 0 && page > totalPages)
            {
                page = totalPages;
            }

            var expenses = await query
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.Id)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            var expenseIds = expenses.Select(e => e.Id).ToList();

            var workflowInstances = await _context.WorkflowInstances
                .Where(i => i.DocumentType == WorkflowDocumentType.AssetExpense && expenseIds.Contains(i.DocumentId))
                .Include(i => i.Actions)
                    .ThenInclude(a => a.User)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var workflowLookup = workflowInstances
                .GroupBy(i => i.DocumentId)
                .ToDictionary(g => g.Key, g => g.First());

            var references = expenses
                .Select(e => $"ASSETEXP:{e.Id}")
                .ToList();

            var journalEntries = await _context.JournalEntries
                .Where(j => j.Reference != null && references.Contains(j.Reference))
                .Select(j => new { j.Reference, j.Id, j.Number })
                .ToListAsync();

            var journalEntryLookup = journalEntries
                .ToDictionary(j => j.Reference!, j => (j.Id, j.Number));

            var items = expenses.Select(e =>
            {
                var reference = $"ASSETEXP:{e.Id}";
                int? journalEntryId = null;
                string? journalEntryNumber = null;

                if (journalEntryLookup.TryGetValue(reference, out var entryInfo))
                {
                    journalEntryId = entryInfo.Id;
                    journalEntryNumber = entryInfo.Number;
                }

                workflowLookup.TryGetValue(e.Id, out var instance);

                return new AssetExpenseListViewModel
                {
                    Id = e.Id,
                    AssetName = e.Asset.Name,
                    BranchName = e.Asset.Branch.NameAr,
                    ExpenseAccountName = e.ExpenseAccount.NameAr,
                    SupplierName = e.Supplier.NameAr,
                    CreatedByName = e.CreatedBy == null
                        ? null
                        : (string.IsNullOrWhiteSpace(e.CreatedBy.FullName) ? e.CreatedBy.UserName : e.CreatedBy.FullName),
                    ApprovedByName = instance?.Actions?
                        .Where(a => a.Status == WorkflowActionStatus.Approved)
                        .OrderByDescending(a => a.ActionedAt)
                        .Select(a => a.User == null
                            ? null
                            : (string.IsNullOrWhiteSpace(a.User.FullName) ? a.User.UserName : a.User.FullName))
                        .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name)),
                    Amount = e.Amount,
                    IsCash = e.IsCash,
                    Date = e.Date,
                    Notes = e.Notes,
                    AttachmentFileName = e.AttachmentFileName,
                    AttachmentFilePath = AttachmentPathHelper.NormalizeForClient(e.AttachmentFilePath),
                    JournalEntryId = journalEntryId,
                    JournalEntryNumber = journalEntryNumber,
                    WorkflowStatus = instance?.Status,
                    RejectionReason = instance?.Actions?
                        .Where(a => a.Status == WorkflowActionStatus.Rejected)
                        .OrderByDescending(a => a.ActionedAt)
                        .Select(a => a.Notes)
                        .FirstOrDefault(n => !string.IsNullOrWhiteSpace(n))
                };
            }).ToList();

            var model = new PagedResult<AssetExpenseListViewModel>
            {
                Items = items,
                Page = page,
                PageSize = pageSize,
                TotalItems = totalItems,
                SearchTerm = trimmedSearch
            };

            ViewBag.ShowMyExpenses = showMyExpenses;
            ViewBag.PageSize = pageSize;

            return View(model);
        }

        [Authorize(Policy = "assetexpenses.view")]
        public async Task<IActionResult> ExportExcel(string? searchTerm, bool showMyExpenses = false)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var userBranchIds = await GetUserBranchIdsAsync(user.Id);

            var query = _context.AssetExpenses
                .AsNoTracking()
                .Include(e => e.Asset).ThenInclude(a => a.Branch)
                .Include(e => e.ExpenseAccount)
                .Include(e => e.Supplier)
                .Include(e => e.CreatedBy)
                .AsQueryable();

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

            var trimmedSearch = string.IsNullOrWhiteSpace(searchTerm) ? null : searchTerm.Trim();

            if (showMyExpenses)
            {
                var currentUserId = _userManager.GetUserId(User);
                if (string.IsNullOrEmpty(currentUserId))
                {
                    return Challenge();
                }

                query = query.Where(e => e.CreatedById == currentUserId);
            }

            if (!string.IsNullOrEmpty(trimmedSearch))
            {
                var likeFilter = $"%{trimmedSearch}%";
                query = query.Where(e =>
                    EF.Functions.Like(e.Asset.Name, likeFilter) ||
                    EF.Functions.Like(e.Asset.Branch.NameAr, likeFilter) ||
                    EF.Functions.Like(e.ExpenseAccount.NameAr, likeFilter) ||
                    EF.Functions.Like(e.Supplier.NameAr, likeFilter) ||
                    (e.CreatedBy != null && (
                        (!string.IsNullOrWhiteSpace(e.CreatedBy.FullName) && EF.Functions.Like(e.CreatedBy.FullName!, likeFilter)) ||
                        (!string.IsNullOrWhiteSpace(e.CreatedBy.UserName) && EF.Functions.Like(e.CreatedBy.UserName!, likeFilter))
                    )) ||
                    (!string.IsNullOrWhiteSpace(e.Notes) && EF.Functions.Like(e.Notes!, likeFilter)));
            }

            var expenses = await query
                .OrderByDescending(e => e.Date)
                .ThenByDescending(e => e.Id)
                .ToListAsync();

            var expenseIds = expenses.Select(e => e.Id).ToList();

            var workflowInstances = await _context.WorkflowInstances
                .AsNoTracking()
                .Where(i => i.DocumentType == WorkflowDocumentType.AssetExpense && expenseIds.Contains(i.DocumentId))
                .Include(i => i.Actions)
                    .ThenInclude(a => a.User)
                .OrderByDescending(i => i.CreatedAt)
                .ToListAsync();

            var workflowLookup = workflowInstances
                .GroupBy(i => i.DocumentId)
                .ToDictionary(g => g.Key, g => g.First());

            var references = expenses
                .Select(e => $"ASSETEXP:{e.Id}")
                .ToList();

            var journalEntries = await _context.JournalEntries
                .AsNoTracking()
                .Where(j => j.Reference != null && references.Contains(j.Reference))
                .Select(j => new { j.Reference, j.Number })
                .ToListAsync();

            var journalLookup = journalEntries
                .ToDictionary(j => j.Reference!, j => j.Number);

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("AssetExpenses");

            worksheet.Cell(1, 1).Value = "تاريخ العملية";
            worksheet.Cell(1, 2).Value = "الأصل";
            worksheet.Cell(1, 3).Value = "الفرع";
            worksheet.Cell(1, 4).Value = "حساب المصروف";
            worksheet.Cell(1, 5).Value = "المورد";
            worksheet.Cell(1, 6).Value = "نوع الدفع";
            worksheet.Cell(1, 7).Value = "المبلغ";
            worksheet.Cell(1, 8).Value = "الملاحظات";
            worksheet.Cell(1, 9).Value = "أنشئ بواسطة";
            worksheet.Cell(1, 10).Value = "آخر معتمد";
            worksheet.Cell(1, 11).Value = "رقم القيد";
            worksheet.Row(1).Style.Font.Bold = true;

            var row = 2;
            foreach (var expense in expenses)
            {
                worksheet.Cell(row, 1).Value = expense.Date;
                worksheet.Cell(row, 1).Style.DateFormat.Format = "yyyy-MM-dd";
                worksheet.Cell(row, 2).Value = expense.Asset.Name;
                worksheet.Cell(row, 3).Value = expense.Asset.Branch.NameAr;
                worksheet.Cell(row, 4).Value = expense.ExpenseAccount.NameAr;
                worksheet.Cell(row, 5).Value = expense.Supplier.NameAr;
                worksheet.Cell(row, 6).Value = expense.IsCash ? "نقدي" : "غير نقدي";
                worksheet.Cell(row, 7).Value = expense.Amount;
                worksheet.Cell(row, 8).Value = expense.Notes ?? string.Empty;
                worksheet.Cell(row, 9).Value = expense.CreatedBy == null
                    ? string.Empty
                    : (string.IsNullOrWhiteSpace(expense.CreatedBy.FullName) ? expense.CreatedBy.UserName : expense.CreatedBy.FullName);

                var reference = $"ASSETEXP:{expense.Id}";
                journalLookup.TryGetValue(reference, out var journalNumber);
                worksheet.Cell(row, 11).Value = journalNumber ?? string.Empty;

                workflowLookup.TryGetValue(expense.Id, out var instance);
                var approverName = instance?.Actions?
                    .Where(a => a.Status == WorkflowActionStatus.Approved)
                    .OrderByDescending(a => a.ActionedAt)
                    .Select(a => a.User == null
                        ? null
                        : (string.IsNullOrWhiteSpace(a.User.FullName) ? a.User.UserName : a.User.FullName))
                    .FirstOrDefault(name => !string.IsNullOrWhiteSpace(name));

                worksheet.Cell(row, 10).Value = approverName ?? string.Empty;

                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"AssetExpenses_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [Authorize(Policy = "assetexpenses.create")]
        public async Task<IActionResult> Create()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            var userBranchIds = await GetUserBranchIdsAsync(user.Id);

            var model = new CreateAssetExpenseViewModel
            {
                Date = DateTime.Now
            };

            await PopulateCreateAssetExpenseModelAsync(model, userBranchIds);

            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assetexpenses.create")]
        public async Task<IActionResult> Create(CreateAssetExpenseViewModel model)
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
            {
                return Challenge();
            }

            model.Date = DateTime.Now;

            var userBranchIds = await GetUserBranchIdsAsync(user.Id);

            var asset = await _context.Assets
                .Include(a => a.Branch)
                .FirstOrDefaultAsync(a => a.Id == model.AssetId);
            if (asset == null)
            {
                ModelState.AddModelError(nameof(model.AssetId), "الأصل غير موجود");
            }
            else if (!asset.AllowAssetExpenses || asset.IsDisposed)
            {
                ModelState.AddModelError(nameof(model.AssetId), "مصروف الأصل غير متاح لهذا الأصل");
            }

            Account? expenseAccount = null;
            var expenseSetting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "AssetExpensesParentAccountId");
            if (!string.IsNullOrEmpty(expenseSetting?.Value) && int.TryParse(expenseSetting.Value, out var expenseParentId))
            {
                expenseAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == model.ExpenseAccountId && a.ParentId == expenseParentId && a.IsActive && a.CanPostTransactions);
            }
            else
            {
                ModelState.AddModelError(string.Empty, "لم يتم ضبط حسابات مصاريف الأصول في الإعدادات");
            }

            if (expenseAccount == null)
            {
                ModelState.AddModelError(nameof(model.ExpenseAccountId), "حساب المصروف غير صالح");
            }

            Supplier? supplier = null;
            if (!model.SupplierId.HasValue)
            {
                ModelState.AddModelError(nameof(model.SupplierId), "الرجاء اختيار المورد");
            }
            else
            {
                supplier = await _context.Suppliers
                    .Include(s => s.Account)
                        .ThenInclude(a => a.Currency)
                    .Include(s => s.SupplierBranches)
                    .FirstOrDefaultAsync(s => s.Id == model.SupplierId.Value);

                if (supplier?.Account == null || !supplier.IsActive || !supplier.Account.IsActive)
                {
                    ModelState.AddModelError(nameof(model.SupplierId), "المورد غير موجود أو لا يملك حساباً");
                }
                else if (!supplier.AuthorizedOperations.HasFlag(SupplierAuthorization.AssetExpense)
                    || !supplier.SupplierBranches.Any(sb => userBranchIds.Contains(sb.BranchId)))
                {
                    ModelState.AddModelError(nameof(model.SupplierId), "المورد المحدد غير مسموح له بهذه العملية أو غير مرتبط بفرعك");
                }
            }

            if (expenseAccount != null)
            {
                model.CurrencyId = expenseAccount.CurrencyId;
                if (supplier?.Account != null && supplier.Account.CurrencyId != expenseAccount.CurrencyId)
                {
                    ModelState.AddModelError(nameof(model.SupplierId), "يجب أن تكون الحسابات بنفس العملة");
                }
                if (model.IsCash && user.PaymentAccountId.HasValue)
                {
                    var paymentAccount = await _context.Accounts
                        .AsNoTracking()
                        .FirstOrDefaultAsync(a => a.Id == user.PaymentAccountId.Value);

                    if (paymentAccount == null)
                    {
                        ModelState.AddModelError(string.Empty, "حساب الدفع غير موجود");
                    }
                    else if (!paymentAccount.IsActive)
                    {
                        ModelState.AddModelError(string.Empty, "حساب الدفع غير نشط");
                    }
                    else
                    {
                        if (paymentAccount.CurrencyId != expenseAccount.CurrencyId)
                        {
                            ModelState.AddModelError(string.Empty, "يجب أن تكون الحسابات بنفس العملة");
                        }

                        if (!paymentAccount.HasSufficientCashBalance(model.Amount))
                        {
                            ModelState.AddModelError(nameof(model.Amount), AssetExpenseMessages.InsufficientPaymentBalanceMessage);
                        }
                    }
                }
            }

            if (model.IsCash && (!user.PaymentAccountId.HasValue || !user.PaymentBranchId.HasValue))
            {
                ModelState.AddModelError(string.Empty, "لا يوجد حساب/فرع للدفع مضبوط للمستخدم");
            }

            if (model.IsCash)
            {
                var chashacc = await _context.Accounts.FirstOrDefaultAsync(t => t.Id == user.PaymentAccountId);

                if (chashacc?.CurrentBalance < model.Amount)
                {
                    ModelState.AddModelError(string.Empty, "لا يوجد رصيد كافي في الصندوق");

                }
            }
            if (!ModelState.IsValid)
            {
                await PopulateCreateAssetExpenseModelAsync(model, userBranchIds);
                return View(model);
            }

            if (model.ExchangeRate <= 0)
            {
                var currency = await _context.Currencies.FindAsync(model.CurrencyId);
                model.ExchangeRate = currency?.ExchangeRate ?? 1m;
            }

            var attachmentResult = await _attachmentStorageService.SaveAsync(model.Attachment, "asset-expenses");

            var assetExpense = new AssetExpense
            {
                AssetId = model.AssetId,
                ExpenseAccountId = model.ExpenseAccountId,
                SupplierId = model.SupplierId!.Value,
                CurrencyId = model.CurrencyId,
                Amount = model.Amount,
                ExchangeRate = model.ExchangeRate,
                Date = model.Date,
                Notes = model.Notes,
                AttachmentFileName = attachmentResult?.FileName,
                AttachmentFilePath = attachmentResult?.FilePath,
                IsCash = model.IsCash,
                CreatedById = user.Id
            };

            if (assetExpense.IsCash)
            {

                assetExpense.AccountId = user.PaymentAccountId;
            }

            _context.AssetExpenses.Add(assetExpense);
            await _context.SaveChangesAsync();

            var branchId = asset.BranchId;
            var definition = await _workflowService.GetActiveDefinitionAsync(WorkflowDocumentType.AssetExpense, branchId);

            if (definition != null)
            {
                var baseAmount = assetExpense.Amount * assetExpense.ExchangeRate;
                var instance = await _workflowService.StartWorkflowAsync(
                    definition,
                    WorkflowDocumentType.AssetExpense,
                    assetExpense.Id,
                    user.Id,
                    branchId,
                    assetExpense.Amount,
                    baseAmount,
                    assetExpense.CurrencyId);

                if (instance != null)
                {
                    assetExpense.WorkflowInstanceId = instance.Id;
                    await _context.SaveChangesAsync();
                    TempData["InfoMessage"] = "تم إرسال مصروف الأصل لاعتمادات الموافقة";
                    return RedirectToAction(nameof(Index));
                }
            }

            return await FinalizeAssetExpenseAsync(assetExpense, user, userBranchIds, model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "assetexpenses.delete")]
        public async Task<IActionResult> Delete(int id)
        {
            var expense = await _context.AssetExpenses.FirstOrDefaultAsync(e => e.Id == id);
            if (expense == null)
            {
                return NotFound();
            }

            var reference = $"ASSETEXP:{id}";
            var journalEntries = await _context.JournalEntries
                .Include(j => j.Lines)
                    .ThenInclude(l => l.Account)
                .Where(j => j.Reference == reference)
                .ToListAsync();

            foreach (var entry in journalEntries.Where(e => e.Status == JournalEntryStatus.Posted))
            {
                foreach (var line in entry.Lines)
                {
                    if (line.Account == null)
                    {
                        continue;
                    }

                    var account = line.Account;
                    var netAmount = account.Nature == AccountNature.Debit
                        ? line.DebitAmount - line.CreditAmount
                        : line.CreditAmount - line.DebitAmount;

                    account.CurrentBalance -= netAmount;
                    account.UpdatedAt = DateTime.Now;
                }
            }

            if (journalEntries.Any())
            {
                _context.JournalEntries.RemoveRange(journalEntries);
            }

            var workflowInstance = await _context.WorkflowInstances
                .Include(i => i.Actions)
                .FirstOrDefaultAsync(i => i.DocumentType == WorkflowDocumentType.AssetExpense && i.DocumentId == id);

            if (workflowInstance != null)
            {
                if (workflowInstance.Actions?.Any() == true)
                {
                    _context.WorkflowActions.RemoveRange(workflowInstance.Actions);
                }

                _context.WorkflowInstances.Remove(workflowInstance);
            }

            _attachmentStorageService.Delete(expense.AttachmentFilePath);

            _context.AssetExpenses.Remove(expense);

            await _context.SaveChangesAsync();

            TempData["SuccessMessage"] = "تم حذف مصروف الأصل والقيد المحاسبي المرتبط به.";

            return RedirectToAction(nameof(Index));
        }

        private async Task<IEnumerable<AssetExpenseSupplierOption>> GetSuppliersAsync(IReadOnlyCollection<int> userBranchIds)
        {
            return await _context.Suppliers
                .AsNoTracking()
                .FilterByAuthorizationAndBranches(SupplierAuthorization.AssetExpense, userBranchIds)
                .Where(s => s.IsActive && s.AccountId != null && s.Account != null && s.Account.IsActive)
                .OrderBy(s => s.NameAr)
                .Select(s => new AssetExpenseSupplierOption
                {
                    Id = s.Id,
                    DisplayName = s.NameAr,
                    AccountId = s.AccountId!.Value,
                    CurrencyId = s.Account!.CurrencyId,
                    CurrencyCode = s.Account.Currency.Code
                })
                .ToListAsync();
        }

        private async Task<IActionResult> FinalizeAssetExpenseAsync(AssetExpense assetExpense, User user, IReadOnlyCollection<int> userBranchIds, CreateAssetExpenseViewModel model)
        {
            try
            {
                await _assetExpenseProcessor.FinalizeAsync(assetExpense, user.Id);
                TempData["SuccessMessage"] = AssetExpenseApprovedMessage;
                return RedirectToAction(nameof(Index));
            }
            catch (InvalidOperationException ex) when (ex.Message == AssetExpenseMessages.InsufficientPaymentBalanceMessage)
            {
                await RemoveAssetExpenseAsync(assetExpense);
                ModelState.AddModelError(nameof(model.Amount), ex.Message);
                await PopulateCreateAssetExpenseModelAsync(model, userBranchIds);
                return View("Create", model);
            }
        }

        private async Task RemoveAssetExpenseAsync(AssetExpense assetExpense)
        {
            _attachmentStorageService.Delete(assetExpense.AttachmentFilePath);
            _context.AssetExpenses.Remove(assetExpense);
            await _context.SaveChangesAsync();
        }

        private async Task PopulateCreateAssetExpenseModelAsync(CreateAssetExpenseViewModel model, IReadOnlyCollection<int> userBranchIds)
        {
            model.Assets = await GetAssetsAsync(userBranchIds);
            model.ExpenseAccounts = await GetExpenseAccountsAsync();
            model.Suppliers = await GetSuppliersAsync(userBranchIds);
        }

        private async Task<IEnumerable<SelectListItem>> GetAssetsAsync(IReadOnlyCollection<int> userBranchIds)
        {
            var query = _context.Assets
                .Include(a => a.Branch)
                .Where(a => a.AllowAssetExpenses && !a.IsDisposed)
                .AsQueryable();

            //if (userBranchIds.Any())
            //{
            //    query = query.Where(a => userBranchIds.Contains(a.BranchId));
            //}

            return await query
                .OrderBy(a => a.Name)
                .Select(a => new SelectListItem
                {
                    Value = a.Id.ToString(),
                    Text = $"{a.Name} - {a.Branch.NameAr}"
                }).ToListAsync();
        }

        private async Task<IEnumerable<AssetExpenseAccountOption>> GetExpenseAccountsAsync()
        {
            var setting = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == "AssetExpensesParentAccountId");
            if (setting != null && int.TryParse(setting.Value, out var parentAccountId))
            {

                var acc = await _context.Accounts.FirstOrDefaultAsync(t => t.Id == parentAccountId);
                if (acc == null)
                {
                    return Enumerable.Empty<AssetExpenseAccountOption>();
                }
                return await _context.Accounts
                    .Where(a => a.ParentId == acc.Id && a.IsActive && a.CanPostTransactions)
                    .Include(a => a.Currency)
                    .OrderBy(a => a.Code)
                    .Select(a => new AssetExpenseAccountOption
                    {
                        Id = a.Id,
                        DisplayName = $"{a.Code} - {a.NameAr} ({a.Currency.Code})",
                        CurrencyId = a.CurrencyId,
                        CurrencyCode = a.Currency.Code
                    }).ToListAsync();
            }

            return Enumerable.Empty<AssetExpenseAccountOption>();
        }

    }
}
