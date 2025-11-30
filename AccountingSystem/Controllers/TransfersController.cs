using AccountingSystem.Data;
using AccountingSystem.Extensions;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;
using AccountingSystem.Services;
using ClosedXML.Excel;
using System.Globalization;
using System.IO;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "transfers.view")]
    public class TransfersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;
        private readonly IAuthorizationService _authorizationService;
        private readonly IJournalEntryService _journalEntryService;
        private readonly IAttachmentStorageService _attachmentStorageService;
        private const string IntermediaryAccountSettingKeyPrefix = "TransferIntermediaryAccountId";

        public TransfersController(ApplicationDbContext context, UserManager<User> userManager, IAuthorizationService authorizationService, IJournalEntryService journalEntryService, IAttachmentStorageService attachmentStorageService)
        {
            _context = context;
            _userManager = userManager;
            _authorizationService = authorizationService;
            _journalEntryService = journalEntryService;
            _attachmentStorageService = attachmentStorageService;
        }

        public async Task<IActionResult> Index()
        {
            var user = await _userManager.GetUserAsync(User);
            if (user == null)
                return Challenge();

            var transfersQuery = _context.PaymentTransfers
                .Include(t => t.Sender)
                .Include(t => t.Receiver)
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .AsQueryable();

            if (user.PaymentBranchId.HasValue)
            {
                var branchId = user.PaymentBranchId.Value;
                transfersQuery = transfersQuery.Where(t =>
                    (t.FromBranchId.HasValue && t.FromBranchId.Value == branchId) ||
                    (t.ToBranchId.HasValue && t.ToBranchId.Value == branchId));
            }
            else
            {
                transfersQuery = transfersQuery.Where(t => t.SenderId == user.Id || t.ReceiverId == user.Id);
            }

            var transfers = await transfersQuery
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            await PrepareTransferViewBagsAsync(transfers);

            ViewBag.CurrentUserId = user.Id;
            ViewBag.CanManageTransfers = await HasManagePermissionAsync();
            ViewBag.IsManageView = false;
            return View(transfers);
        }

        [Authorize(Policy = "transfers.manage")]
        public async Task<IActionResult> Manage([FromQuery] TransferManagementFilters? filters)
        {
            filters ??= new TransferManagementFilters();
            filters.Normalize();

            var transfersQuery = _context.PaymentTransfers
                .AsNoTracking()
                .Include(t => t.Sender)
                .Include(t => t.Receiver)
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .AsQueryable();

            transfersQuery = ApplyTransferManagementFilters(transfersQuery, filters);

            var transfers = await transfersQuery
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            await PrepareTransferViewBagsAsync(transfers);

            var branchOptions = await _context.Branches
                .AsNoTracking()
                .OrderBy(b => b.NameAr)
                .Select(b => new SelectListItem
                {
                    Value = b.Id.ToString(),
                    Text = b.NameAr
                })
                .ToListAsync();

            var viewModel = new TransferManagementViewModel
            {
                Filters = filters,
                Transfers = transfers,
                Branches = branchOptions
            };

            ViewBag.CurrentUserId = _userManager.GetUserId(User);
            ViewBag.CanManageTransfers = true;
            ViewBag.IsManageView = true;
            return View(viewModel);
        }

        [Authorize(Policy = "transfers.manage")]
        public async Task<IActionResult> ManageExportExcel([FromQuery] TransferManagementFilters? filters)
        {
            filters ??= new TransferManagementFilters();
            filters.Normalize();

            var transfersQuery = _context.PaymentTransfers
                .AsNoTracking()
                .Include(t => t.Sender)
                .Include(t => t.Receiver)
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .AsQueryable();

            transfersQuery = ApplyTransferManagementFilters(transfersQuery, filters);

            var transfers = await transfersQuery
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();

            using var workbook = new XLWorkbook();
            var worksheet = workbook.Worksheets.Add("Transfers");

            worksheet.Cell(1, 1).Value = "المرسل";
            worksheet.Cell(1, 2).Value = "فرع الإرسال";
            worksheet.Cell(1, 3).Value = "المستلم";
            worksheet.Cell(1, 4).Value = "فرع الاستقبال";
            worksheet.Cell(1, 5).Value = "المبلغ";
            worksheet.Cell(1, 6).Value = "الحالة";
            worksheet.Cell(1, 7).Value = "التاريخ";
            worksheet.Cell(1, 8).Value = "ملاحظات";
            worksheet.Row(1).Style.Font.Bold = true;

            var statusLabels = new Dictionary<TransferStatus, string>
            {
                [TransferStatus.Pending] = "قيد الانتظار",
                [TransferStatus.Accepted] = "مقبولة",
                [TransferStatus.Rejected] = "مرفوضة"
            };

            var row = 2;
            foreach (var transfer in transfers)
            {
                worksheet.Cell(row, 1).Value = transfer.Sender?.FullName ?? transfer.SenderId;
                worksheet.Cell(row, 2).Value = transfer.FromBranch?.NameAr ?? string.Empty;
                worksheet.Cell(row, 3).Value = transfer.Receiver?.FullName ?? transfer.ReceiverId;
                worksheet.Cell(row, 4).Value = transfer.ToBranch?.NameAr ?? string.Empty;
                worksheet.Cell(row, 5).Value = transfer.Amount;
                worksheet.Cell(row, 6).Value = statusLabels.TryGetValue(transfer.Status, out var label) ? label : transfer.Status.ToString();
                worksheet.Cell(row, 7).Value = transfer.CreatedAt;
                worksheet.Cell(row, 7).Style.DateFormat.Format = "yyyy-MM-dd";
                worksheet.Cell(row, 8).Value = transfer.Notes ?? string.Empty;
                row++;
            }

            worksheet.Columns().AdjustToContents();

            using var stream = new MemoryStream();
            workbook.SaveAs(stream);
            stream.Position = 0;

            var fileName = $"Transfers_{DateTime.Now:yyyyMMddHHmmss}.xlsx";
            return File(stream.ToArray(), "application/vnd.openxmlformats-officedocument.spreadsheetml.sheet", fileName);
        }

        [Authorize(Policy = "transfers.create")]
        public async Task<IActionResult> Create()
        {
            var sender = await _userManager.GetUserAsync(User);
            if (sender == null)
                return Challenge();

            var model = new TransferCreateViewModel();
            await PopulateCreateViewModelAsync(model, sender.Id);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "transfers.create")]
        public async Task<IActionResult> Create(TransferCreateViewModel model)
        {
            var sender = await _userManager.GetUserAsync(User);
            if (sender == null)
                return Challenge();

            if (!ModelState.IsValid)
            {
                await PopulateCreateViewModelAsync(model, sender.Id);
                return View(model);
            }

            if (!model.FromPaymentAccountId.HasValue)
            {
                ModelState.AddModelError(nameof(model.FromPaymentAccountId), "الرجاء اختيار حساب الإرسال");
                await PopulateCreateViewModelAsync(model, sender.Id);
                return View(model);
            }

            var senderAccount = await _context.UserPaymentAccounts
                .Include(u => u.Account).ThenInclude(a => a.Currency)
                .FirstOrDefaultAsync(u => u.UserId == sender.Id && u.AccountId == model.FromPaymentAccountId.Value);
            if (senderAccount == null)
            {
                ModelState.AddModelError(nameof(model.FromPaymentAccountId), "الحساب المحدد غير متاح للمستخدم");
                await PopulateCreateViewModelAsync(model, sender.Id);
                return View(model);
            }

            if (senderAccount.Account == null)
            {
                ModelState.AddModelError(string.Empty, "تعذر تحميل حساب الإرسال المحدد.");
                await PopulateCreateViewModelAsync(model, sender.Id);
                return View(model);
            }

            var (intermediaryAccount, intermediaryError) = await GetIntermediaryAccountAsync(senderAccount.Account.CurrencyId);
            if (intermediaryAccount == null)
            {
                ModelState.AddModelError(string.Empty, intermediaryError ?? "لم يتم إعداد حساب الوسيط للحوالات.");
                await PopulateCreateViewModelAsync(model, sender.Id);
                return View(model);
            }

            if (intermediaryAccount.CurrencyId != senderAccount.Account.CurrencyId)
            {
                ModelState.AddModelError(string.Empty, "عملة حساب الوسيط يجب أن تطابق عملة حساب الإرسال.");
                await PopulateCreateViewModelAsync(model, sender.Id);
                return View(model);
            }

            var receiver = await _context.Users
                .Include(u => u.PaymentBranch)
                .FirstOrDefaultAsync(u => u.Id == model.ReceiverId);
            if (receiver == null || receiver.Id == sender.Id)
            {
                ModelState.AddModelError(nameof(model.ReceiverId), receiver == null ? "المستلم غير موجود" : "لا يمكن إرسال حوالة لنفسك");
                await PopulateCreateViewModelAsync(model, sender.Id);
                return View(model);
            }

            var receiverAccount = await _context.UserPaymentAccounts
                .Include(u => u.Account)
                .FirstOrDefaultAsync(u => u.UserId == receiver.Id && u.CurrencyId == senderAccount.CurrencyId);
            if (receiverAccount == null)
            {
                ModelState.AddModelError(nameof(model.ReceiverId), "لا يوجد للمستلم حساب بنفس عملة الحساب المرسل");
                await PopulateCreateViewModelAsync(model, sender.Id);
                return View(model);
            }

            var breakdownMap = model.CurrencyUnitCounts?
                .Where(c => c.CurrencyUnitId > 0 && c.Count > 0)
                .GroupBy(c => c.CurrencyUnitId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Count))
                ?? new Dictionary<int, int>();

            decimal finalAmount = model.Amount;
            string? breakdownJson = null;

            if (breakdownMap.Any())
            {
                var units = await _context.CurrencyUnits
                    .Where(u => breakdownMap.Keys.Contains(u.Id))
                    .Select(u => new { u.Id, u.CurrencyId, u.ValueInBaseUnit })
                    .ToListAsync();

                if (units.Count != breakdownMap.Count || units.Any(u => u.CurrencyId != senderAccount.Account.CurrencyId))
                {
                    ModelState.AddModelError(string.Empty, "بيانات الفئات غير صحيحة.");
                    await PopulateCreateViewModelAsync(model, sender.Id);
                    return View(model);
                }

                decimal amountFromUnits = 0m;
                foreach (var unit in units)
                {
                    amountFromUnits += unit.ValueInBaseUnit * breakdownMap[unit.Id];
                }

                if (amountFromUnits <= 0)
                {
                    ModelState.AddModelError(string.Empty, "المبلغ الناتج عن الفئات يجب أن يكون أكبر من صفر.");
                    await PopulateCreateViewModelAsync(model, sender.Id);
                    return View(model);
                }

                finalAmount = Math.Round(amountFromUnits, 2, MidpointRounding.AwayFromZero);
                model.Amount = finalAmount;
                breakdownJson = JsonSerializer.Serialize(breakdownMap);
            }

            if (senderAccount.Account.Nature == AccountNature.Debit && finalAmount > senderAccount.Account.CurrentBalance)
            {
                ModelState.AddModelError(nameof(model.Amount), "الرصيد المتاح في حساب الإرسال لا يكفي لإتمام العملية.");
                await PopulateCreateViewModelAsync(model, sender.Id);
                return View(model);
            }

            var transfer = new PaymentTransfer
            {
                SenderId = sender.Id,
                ReceiverId = receiver.Id,
                FromPaymentAccountId = model.FromPaymentAccountId.Value,
                ToPaymentAccountId = receiverAccount.AccountId,
                FromBranchId = sender.PaymentBranchId,
                ToBranchId = receiver.PaymentBranchId,
                Amount = finalAmount,
                Notes = model.Notes,
                Status = TransferStatus.Pending,
                CreatedAt = DateTime.Now,
                CurrencyBreakdownJson = breakdownJson
            };

            var attachmentResult = await _attachmentStorageService.SaveAsync(model.Attachment, "transfers");
            if (attachmentResult != null)
            {
                transfer.AttachmentFilePath = attachmentResult.FilePath;
                transfer.AttachmentFileName = attachmentResult.FileName;
            }

            using var transaction = await _context.Database.BeginTransactionAsync();
            try
            {
                _context.PaymentTransfers.Add(transfer);

                var senderEntryDescription = BuildSenderEntryDescription(receiver, transfer.Notes);
                var senderEntry = await CreateSenderJournalEntryAsync(transfer, intermediaryAccount, sender.Id, senderEntryDescription);

                transfer.SenderJournalEntry = senderEntry;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();
            }
            catch
            {
                await transaction.RollbackAsync();
                throw;
            }

            var returnUrl = Url.Action(nameof(Index)) ?? Url.Content("~/");
            var printUrl = Url.Action(nameof(PrintSend), new { id = transfer.Id, returnUrl });
            if (!string.IsNullOrEmpty(printUrl))
                TempData["PrintUrl"] = printUrl;

            return Redirect(returnUrl);
        }

        [Authorize]
        public async Task<IActionResult> Approve(int id, bool accept, string? returnUrl)
        {
            var approvePermission = await _authorizationService.AuthorizeAsync(User, "transfers.approve");
            var canManage = await HasManagePermissionAsync();
            if (!approvePermission.Succeeded && !canManage)
                return Forbid();

            var currentUserId = _userManager.GetUserId(User);
            var transfer = await _context.PaymentTransfers
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .Include(t => t.Sender)
                .Include(t => t.Receiver)
                .Include(t => t.SenderJournalEntry).ThenInclude(e => e!.Lines)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null || transfer.Status != TransferStatus.Pending)
                return NotFound();

            if (!canManage && transfer.ReceiverId != currentUserId)
                return NotFound();

            using var transaction = await _context.Database.BeginTransactionAsync();

            if (accept)
            {
                var receiverAccount = await _context.Accounts
                    .Include(a => a.Currency)
                    .FirstOrDefaultAsync(a => a.Id == transfer.ToPaymentAccountId);
                if (receiverAccount == null)
                {
                    TempData["ErrorMessage"] = "حساب المستلم غير موجود.";
                    await transaction.RollbackAsync();
                    return RedirectAfterAction(returnUrl);
                }

                var (intermediaryAccount, intermediaryError) = await GetIntermediaryAccountAsync(receiverAccount.CurrencyId);
                if (intermediaryAccount == null)
                {
                    TempData["ErrorMessage"] = intermediaryError ?? "لم يتم إعداد حساب الوسيط للحوالات.";
                    await transaction.RollbackAsync();
                    return RedirectAfterAction(returnUrl);
                }

                if (intermediaryAccount.CurrencyId != receiverAccount.CurrencyId)
                {
                    TempData["ErrorMessage"] = "عملة حساب الوسيط يجب أن تطابق عملة حساب المستلم.";
                    await transaction.RollbackAsync();
                    return RedirectAfterAction(returnUrl);
                }

                var receiverDescription = BuildReceiverEntryDescription(transfer.Sender, transfer.Notes);
                var createdBy = currentUserId ?? transfer.ReceiverId;
                var entry = await CreateReceiverJournalEntryAsync(transfer, intermediaryAccount, createdBy, receiverDescription);

                transfer.JournalEntry = entry;
                transfer.JournalEntryId = entry.Id;
                transfer.Status = TransferStatus.Accepted;

                await _context.SaveChangesAsync();
                await transaction.CommitAsync();

                var safeReturnUrl = !string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl)
                    ? returnUrl
                    : Url.Action(nameof(Index)) ?? Url.Content("~/");

                var printUrl = Url.Action(nameof(PrintReceive), new { id = transfer.Id, returnUrl = safeReturnUrl });
                if (!string.IsNullOrEmpty(printUrl))
                    TempData["PrintUrl"] = printUrl;
                return RedirectAfterAction(safeReturnUrl);
            }

            if (transfer.SenderJournalEntryId.HasValue && transfer.SenderJournalEntry != null)
            {
                await ReverseAccountBalances(transfer.SenderJournalEntry);
                _context.JournalEntryLines.RemoveRange(transfer.SenderJournalEntry.Lines);
                _context.JournalEntries.Remove(transfer.SenderJournalEntry);
                transfer.SenderJournalEntryId = null;
            }

            transfer.Status = TransferStatus.Rejected;
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();
            return RedirectAfterAction(returnUrl);
        }

        [Authorize]
        public async Task<IActionResult> Edit(int id, string? returnUrl)
        {
            var canManage = await HasManagePermissionAsync();
            var canCreate = (await _authorizationService.AuthorizeAsync(User, "transfers.create")).Succeeded;
            if (!canManage && !canCreate)
                return Forbid();

            var userId = _userManager.GetUserId(User);
            var transfer = await _context.PaymentTransfers
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null || transfer.Status != TransferStatus.Pending || (!canManage && transfer.SenderId != userId))
                return NotFound();

            var model = new TransferEditViewModel
            {
                Id = transfer.Id,
                ReceiverId = transfer.ReceiverId,
                Amount = transfer.Amount,
                Notes = transfer.Notes,
                ReturnUrl = returnUrl,
                ExistingAttachmentPath = AttachmentPathHelper.NormalizeForClient(transfer.AttachmentFilePath),
                ExistingAttachmentName = transfer.AttachmentFileName
            };

            if (!await PopulateEditViewModelAsync(model, transfer))
                return NotFound();

            ViewBag.UserBranches = JsonSerializer.Serialize(model.ReceiverBranches);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Edit(TransferEditViewModel model)
        {
            var canManage = await HasManagePermissionAsync();
            var canCreate = (await _authorizationService.AuthorizeAsync(User, "transfers.create")).Succeeded;
            if (!canManage && !canCreate)
                return Forbid();

            var userId = _userManager.GetUserId(User);
            var transfer = await _context.PaymentTransfers
                .Include(t => t.FromBranch)
                .Include(t => t.Sender)
                .FirstOrDefaultAsync(t => t.Id == model.Id);
            if (transfer == null || transfer.Status != TransferStatus.Pending || (!canManage && transfer.SenderId != userId))
                return NotFound();

            if (!ModelState.IsValid)
            {
                if (!await PopulateEditViewModelAsync(model, transfer))
                    return NotFound();
                ViewBag.UserBranches = JsonSerializer.Serialize(model.ReceiverBranches);
                return View(model);
            }

            var receiver = await _context.Users.Include(u => u.PaymentBranch).FirstOrDefaultAsync(u => u.Id == model.ReceiverId);
            if (receiver == null || receiver.Id == transfer.SenderId)
            {
                ModelState.AddModelError(nameof(model.ReceiverId), receiver == null ? "المستلم غير موجود" : "لا يمكن إرسال حوالة لنفسك");
                if (!await PopulateEditViewModelAsync(model, transfer))
                    return NotFound();
                ViewBag.UserBranches = JsonSerializer.Serialize(model.ReceiverBranches);
                return View(model);
            }

            var fromAccount = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == transfer.FromPaymentAccountId);
            if (fromAccount == null)
            {
                ModelState.AddModelError(string.Empty, "حساب الإرسال غير موجود");
                if (!await PopulateEditViewModelAsync(model, transfer))
                    return NotFound();
                ViewBag.UserBranches = JsonSerializer.Serialize(model.ReceiverBranches);
                return View(model);
            }

            var receiverAccount = await _context.UserPaymentAccounts
                .Include(u => u.Account)
                .FirstOrDefaultAsync(u => u.UserId == receiver.Id && u.CurrencyId == fromAccount.CurrencyId);
            if (receiverAccount == null)
            {
                ModelState.AddModelError(nameof(model.ReceiverId), "لا يوجد للمستلم حساب بنفس عملة الحساب المرسل");
                if (!await PopulateEditViewModelAsync(model, transfer))
                    return NotFound();
                ViewBag.UserBranches = JsonSerializer.Serialize(model.ReceiverBranches);
                return View(model);
            }

            var breakdownMap = model.CurrencyUnitCounts?
                .Where(c => c.CurrencyUnitId > 0 && c.Count > 0)
                .GroupBy(c => c.CurrencyUnitId)
                .ToDictionary(g => g.Key, g => g.Sum(x => x.Count))
                ?? new Dictionary<int, int>();

            decimal finalAmount = model.Amount;
            string? breakdownJson = null;

            if (breakdownMap.Any())
            {
                var units = await _context.CurrencyUnits
                    .Where(u => breakdownMap.Keys.Contains(u.Id))
                    .Select(u => new { u.Id, u.CurrencyId, u.ValueInBaseUnit })
                    .ToListAsync();

                if (units.Count != breakdownMap.Count || units.Any(u => u.CurrencyId != fromAccount.CurrencyId))
                {
                    ModelState.AddModelError(string.Empty, "بيانات الفئات غير صحيحة.");
                    if (!await PopulateEditViewModelAsync(model, transfer))
                        return NotFound();
                    ViewBag.UserBranches = JsonSerializer.Serialize(model.ReceiverBranches);
                    return View(model);
                }

                decimal amountFromUnits = 0m;
                foreach (var unit in units)
                {
                    amountFromUnits += unit.ValueInBaseUnit * breakdownMap[unit.Id];
                }

                if (amountFromUnits <= 0)
                {
                    ModelState.AddModelError(string.Empty, "المبلغ الناتج عن الفئات يجب أن يكون أكبر من صفر.");
                    if (!await PopulateEditViewModelAsync(model, transfer))
                        return NotFound();
                    ViewBag.UserBranches = JsonSerializer.Serialize(model.ReceiverBranches);
                    return View(model);
                }

                finalAmount = Math.Round(amountFromUnits, 2, MidpointRounding.AwayFromZero);
                model.Amount = finalAmount;
                breakdownJson = JsonSerializer.Serialize(breakdownMap);
            }

            if (fromAccount.Nature == AccountNature.Debit && finalAmount > fromAccount.CurrentBalance)
            {
                ModelState.AddModelError(nameof(model.Amount), "الرصيد المتاح في حساب الإرسال لا يكفي لإتمام العملية.");
                if (!await PopulateEditViewModelAsync(model, transfer))
                    return NotFound();
                ViewBag.UserBranches = JsonSerializer.Serialize(model.ReceiverBranches);
                return View(model);
            }

            using var transaction = await _context.Database.BeginTransactionAsync();

            transfer.ReceiverId = receiver.Id;
            transfer.ToPaymentAccountId = receiverAccount.AccountId;
            transfer.ToBranchId = receiver.PaymentBranchId;
            transfer.Amount = finalAmount;
            transfer.Notes = model.Notes;
            transfer.CurrencyBreakdownJson = breakdownJson;

            if (model.RemoveAttachment && !string.IsNullOrEmpty(transfer.AttachmentFilePath))
            {
                _attachmentStorageService.Delete(transfer.AttachmentFilePath);
                transfer.AttachmentFilePath = null;
                transfer.AttachmentFileName = null;
            }

            var newAttachmentResult = await _attachmentStorageService.SaveAsync(model.Attachment, "transfers", transfer.AttachmentFilePath);
            if (newAttachmentResult != null)
            {
                transfer.AttachmentFilePath = newAttachmentResult.FilePath;
                transfer.AttachmentFileName = newAttachmentResult.FileName;
            }

            await UpdateSenderJournalEntryAsync(transfer, receiver, finalAmount, model.Notes ?? string.Empty, fromAccount.CurrencyId);

            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            if (!string.IsNullOrEmpty(model.ReturnUrl) && Url.IsLocalUrl(model.ReturnUrl))
                return Redirect(model.ReturnUrl);

            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize]
        public async Task<IActionResult> Delete(int id, string? returnUrl)
        {
            var canManage = await HasManagePermissionAsync();
            var canCreate = (await _authorizationService.AuthorizeAsync(User, "transfers.create")).Succeeded;
            if (!canManage && !canCreate)
                return Forbid();

            var userId = _userManager.GetUserId(User);
            var transfer = await _context.PaymentTransfers
                .Include(t => t.SenderJournalEntry).ThenInclude(e => e!.Lines)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null || transfer.Status != TransferStatus.Pending || (!canManage && transfer.SenderId != userId))
                return NotFound();

            using var transaction = await _context.Database.BeginTransactionAsync();

            if (transfer.SenderJournalEntry != null)
            {
                await ReverseAccountBalances(transfer.SenderJournalEntry);
                _context.JournalEntryLines.RemoveRange(transfer.SenderJournalEntry.Lines);
                _context.JournalEntries.Remove(transfer.SenderJournalEntry);
            }

            var attachmentPath = transfer.AttachmentFilePath;

            _context.PaymentTransfers.Remove(transfer);
            await _context.SaveChangesAsync();
            await transaction.CommitAsync();

            if (!string.IsNullOrEmpty(attachmentPath))
            {
                _attachmentStorageService.Delete(attachmentPath);
            }

            return RedirectAfterAction(returnUrl);
        }

        [Authorize]
        public async Task<IActionResult> PrintSend(int id, string? returnUrl)
        {
            var transfer = await _context.PaymentTransfers
                .Include(t => t.Sender).ThenInclude(u => u.PaymentBranch)
                .Include(t => t.Receiver).ThenInclude(u => u.PaymentBranch)
                .Include(t => t.FromPaymentAccount).ThenInclude(a => a.Currency)
                .Include(t => t.ToPaymentAccount).ThenInclude(a => a.Currency)
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .Include(t => t.SenderJournalEntry)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null)
                return NotFound();

            if (!await CanAccessTransferAsync(transfer))
                return Forbid();

            var breakdown = ParseBreakdown(transfer.CurrencyBreakdownJson);
            var unitNames = await LoadCurrencyUnitNamesAsync(breakdown);
            Account? intermediaryAccount = null;
            var sendCurrencyId = transfer.FromPaymentAccount?.CurrencyId ?? transfer.ToPaymentAccount?.CurrencyId;
            if (sendCurrencyId.HasValue)
            {
                (intermediaryAccount, _) = await GetIntermediaryAccountAsync(sendCurrencyId.Value);
            }

            var model = new TransferPrintViewModel
            {
                Transfer = transfer,
                CurrencyBreakdown = breakdown,
                CurrencyUnitNames = unitNames,
                IntermediaryAccount = intermediaryAccount,
                ReturnUrl = returnUrl
            };

            return View("PrintSend", model);
        }

        [Authorize]
        public async Task<IActionResult> PrintReceive(int id, string? returnUrl)
        {
            var transfer = await _context.PaymentTransfers
                .Include(t => t.Sender).ThenInclude(u => u.PaymentBranch)
                .Include(t => t.Receiver).ThenInclude(u => u.PaymentBranch)
                .Include(t => t.FromPaymentAccount).ThenInclude(a => a.Currency)
                .Include(t => t.ToPaymentAccount).ThenInclude(a => a.Currency)
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .Include(t => t.SenderJournalEntry)
                .Include(t => t.JournalEntry)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null)
                return NotFound();

            if (transfer.Status != TransferStatus.Accepted)
                return NotFound();

            if (!await CanAccessTransferAsync(transfer))
                return Forbid();

            var breakdown = ParseBreakdown(transfer.CurrencyBreakdownJson);
            var unitNames = await LoadCurrencyUnitNamesAsync(breakdown);
            Account? intermediaryAccount = null;
            var receiveCurrencyId = transfer.ToPaymentAccount?.CurrencyId ?? transfer.FromPaymentAccount?.CurrencyId;
            if (receiveCurrencyId.HasValue)
            {
                (intermediaryAccount, _) = await GetIntermediaryAccountAsync(receiveCurrencyId.Value);
            }

            var model = new TransferPrintViewModel
            {
                Transfer = transfer,
                CurrencyBreakdown = breakdown,
                CurrencyUnitNames = unitNames,
                IntermediaryAccount = intermediaryAccount,
                ReturnUrl = returnUrl
            };

            return View("PrintReceive", model);
        }

        private async Task PopulateCreateViewModelAsync(TransferCreateViewModel model, string senderId)
        {
            var users = await _context.Users
                .Where(u => u.Id != senderId)
                .Include(u => u.PaymentBranch)
                .ToListAsync();

            var receiverItems = users
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = u.FullName ?? $"{u.FirstName} {u.LastName}",
                    Selected = u.Id == model.ReceiverId
                })
                .ToList();

            model.Receivers = receiverItems;
            model.ReceiverBranches = users.ToDictionary(u => u.Id, u => u.PaymentBranch?.NameAr ?? string.Empty);

            var senderAccounts = await _context.UserPaymentAccounts
                .Where(up => up.UserId == senderId)
                .Include(up => up.Account).ThenInclude(a => a.Currency)
                .OrderBy(up => up.Account.NameAr)
                .ToListAsync();

            var accountOptions = senderAccounts
                .Select(up => new TransferCreateViewModel.SenderAccountOption
                {
                    AccountId = up.AccountId,
                    DisplayName = $"{up.Account.Code} - {up.Account.NameAr} ({up.Account.Currency.Code})",
                    CurrencyId = up.Account.CurrencyId,
                    CurrencyCode = up.Account.Currency.Code
                })
                .ToList();

            model.SenderAccounts = accountOptions;

            var currencyIds = senderAccounts
                .Select(up => up.Account.CurrencyId)
                .Distinct()
                .ToList();

            if (currencyIds.Any())
            {
                var currencyUnits = await _context.CurrencyUnits
                    .Where(u => currencyIds.Contains(u.CurrencyId))
                    .OrderBy(u => u.ValueInBaseUnit)
                    .Select(u => new
                    {
                        u.Id,
                        u.CurrencyId,
                        u.Name,
                        u.ValueInBaseUnit
                    })
                    .ToListAsync();

                model.CurrencyUnits = currencyUnits
                    .GroupBy(u => u.CurrencyId)
                    .ToDictionary(
                        g => g.Key,
                        g => g.Select(u => new TransferCreateViewModel.CurrencyUnitOption
                        {
                            CurrencyUnitId = u.Id,
                            Name = u.Name,
                            ValueInBaseUnit = u.ValueInBaseUnit
                        }).ToList()
                    );
            }
            else
            {
                model.CurrencyUnits = new Dictionary<int, List<TransferCreateViewModel.CurrencyUnitOption>>();
            }

            if (!model.FromPaymentAccountId.HasValue && accountOptions.Any())
                model.FromPaymentAccountId = accountOptions.First().AccountId;

            model.SenderBranch = await _context.Users
                .Where(u => u.Id == senderId)
                .Include(u => u.PaymentBranch)
                .Select(u => u.PaymentBranch!.NameAr)
                .FirstOrDefaultAsync() ?? string.Empty;

            var receiverIds = users.Select(u => u.Id).ToList();
            if (receiverIds.Count == 0)
            {
                model.ReceiverAccounts = new Dictionary<string, List<TransferCreateViewModel.ReceiverAccountOption>>();
                return;
            }

            var receiverAccountList = await _context.UserPaymentAccounts
                .Where(up => receiverIds.Contains(up.UserId))
                .Include(up => up.Account).ThenInclude(a => a.Currency)
                .ToListAsync();

            model.ReceiverAccounts = receiverAccountList
                .GroupBy(up => up.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(up => new TransferCreateViewModel.ReceiverAccountOption
                    {
                        AccountId = up.AccountId,
                        CurrencyId = up.CurrencyId,
                        DisplayName = $"{up.Account.Code} - {up.Account.NameAr} ({up.Account.Currency.Code})"
                    }).ToList()
                );
        }

        private async Task<bool> PopulateEditViewModelAsync(TransferEditViewModel model, PaymentTransfer transfer)
        {
            var senderAccount = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == transfer.FromPaymentAccountId);
            if (senderAccount == null)
                return false;

            var users = await _context.Users
                .Where(u => u.Id != transfer.SenderId)
                .Include(u => u.PaymentBranch)
                .ToListAsync();

            model.Receivers = users
                .Select(u => new SelectListItem
                {
                    Value = u.Id,
                    Text = u.FullName ?? $"{u.FirstName} {u.LastName}",
                    Selected = u.Id == model.ReceiverId
                })
                .ToList();

            model.ReceiverBranches = users.ToDictionary(u => u.Id, u => u.PaymentBranch?.NameAr ?? string.Empty);

            var receiverIds = users.Select(u => u.Id).ToList();
            var receiverAccounts = await _context.UserPaymentAccounts
                .Where(up => receiverIds.Contains(up.UserId))
                .Include(up => up.Account).ThenInclude(a => a.Currency)
                .ToListAsync();

            model.ReceiverAccounts = receiverAccounts
                .GroupBy(up => up.UserId)
                .ToDictionary(
                    g => g.Key,
                    g => g.Select(up => new TransferCreateViewModel.ReceiverAccountOption
                    {
                        AccountId = up.AccountId,
                        CurrencyId = up.CurrencyId,
                        DisplayName = $"{up.Account.Code} - {up.Account.NameAr} ({up.Account.Currency.Code})"
                    }).ToList()
                );

            var unitList = await _context.CurrencyUnits
                .Where(u => u.CurrencyId == senderAccount.CurrencyId)
                .OrderBy(u => u.ValueInBaseUnit)
                .Select(u => new TransferCreateViewModel.CurrencyUnitOption
                {
                    CurrencyUnitId = u.Id,
                    Name = u.Name,
                    ValueInBaseUnit = u.ValueInBaseUnit
                })
                .ToListAsync();

            var existingCounts = new Dictionary<int, int>();
            if (model.CurrencyUnitCounts != null && model.CurrencyUnitCounts.Count > 0)
            {
                existingCounts = model.CurrencyUnitCounts
                    .GroupBy(c => c.CurrencyUnitId)
                    .ToDictionary(g => g.Key, g => g.Sum(x => x.Count));
            }
            else if (!string.IsNullOrEmpty(transfer.CurrencyBreakdownJson))
            {
                var parsed = JsonSerializer.Deserialize<Dictionary<int, int>>(transfer.CurrencyBreakdownJson);
                if (parsed != null)
                    existingCounts = parsed;
            }

            model.CurrencyUnits = unitList;
            model.CurrencyUnitCounts = unitList
                .Select(u => new TransferCreateViewModel.CurrencyUnitCountInput
                {
                    CurrencyUnitId = u.CurrencyUnitId,
                    Count = existingCounts.TryGetValue(u.CurrencyUnitId, out var count) ? count : 0
                }).ToList();

            model.CurrencyId = senderAccount.CurrencyId;
            model.CurrencyCode = senderAccount.Currency?.Code ?? string.Empty;
            model.SenderBranch = transfer.FromBranch?.NameAr ?? string.Empty;

            if (string.IsNullOrEmpty(model.ExistingAttachmentPath))
            {
                model.ExistingAttachmentPath = AttachmentPathHelper.NormalizeForClient(transfer.AttachmentFilePath);
            }

            if (string.IsNullOrEmpty(model.ExistingAttachmentName))
            {
                model.ExistingAttachmentName = transfer.AttachmentFileName;
            }

            return true;
        }

        private async Task PrepareTransferViewBagsAsync(List<PaymentTransfer> transfers)
        {
            var breakdowns = new Dictionary<int, Dictionary<int, int>>();
            var unitIds = new HashSet<int>();

            foreach (var transfer in transfers)
            {
                if (string.IsNullOrEmpty(transfer.CurrencyBreakdownJson))
                    continue;

                var parsed = JsonSerializer.Deserialize<Dictionary<int, int>>(transfer.CurrencyBreakdownJson);
                if (parsed == null || parsed.Count == 0)
                    continue;

                breakdowns[transfer.Id] = parsed;
                foreach (var unitId in parsed.Keys)
                {
                    unitIds.Add(unitId);
                }
            }

            Dictionary<int, string> unitNames = new();
            if (unitIds.Count > 0)
            {
                unitNames = await _context.CurrencyUnits
                    .Where(u => unitIds.Contains(u.Id))
                    .OrderByDescending(u => u.ValueInBaseUnit)
                    .ToDictionaryAsync(u => u.Id, u => $"{u.Name} ({u.ValueInBaseUnit:N2})");
            }

            ViewBag.TransferCurrencyBreakdowns = breakdowns;
            ViewBag.CurrencyUnitNames = unitNames;
        }

        private IQueryable<PaymentTransfer> ApplyTransferManagementFilters(
            IQueryable<PaymentTransfer> query,
            TransferManagementFilters filters)
        {
            if (filters.FromBranchId.HasValue)
            {
                var fromBranchId = filters.FromBranchId.Value;
                query = query.Where(t => t.FromBranchId == fromBranchId);
            }

            if (filters.ToBranchId.HasValue)
            {
                var toBranchId = filters.ToBranchId.Value;
                query = query.Where(t => t.ToBranchId == toBranchId);
            }

            if (!string.IsNullOrEmpty(filters.SearchTerm))
            {
                var likeFilter = $"%{filters.SearchTerm}%";
                decimal? numericSearch = null;

                if (decimal.TryParse(filters.SearchTerm, NumberStyles.Number, CultureInfo.InvariantCulture, out var parsedAmount))
                {
                    numericSearch = parsedAmount;
                }
                else if (decimal.TryParse(filters.SearchTerm, NumberStyles.Number, CultureInfo.CurrentCulture, out parsedAmount))
                {
                    numericSearch = parsedAmount;
                }
                else if (decimal.TryParse(filters.SearchTerm, NumberStyles.Number, CultureInfo.GetCultureInfo("ar-SA"), out parsedAmount))
                {
                    numericSearch = parsedAmount;
                }

                query = query.Where(t =>
                    (t.Sender != null && t.Sender.FullName != null && EF.Functions.Like(t.Sender.FullName, likeFilter)) ||
                    (t.Sender != null && t.Sender.UserName != null && EF.Functions.Like(t.Sender.UserName, likeFilter)) ||
                    (t.Receiver != null && t.Receiver.FullName != null && EF.Functions.Like(t.Receiver.FullName, likeFilter)) ||
                    (t.Receiver != null && t.Receiver.UserName != null && EF.Functions.Like(t.Receiver.UserName, likeFilter)) ||
                    (t.FromBranch != null && t.FromBranch.NameAr != null && EF.Functions.Like(t.FromBranch.NameAr, likeFilter)) ||
                    (t.ToBranch != null && t.ToBranch.NameAr != null && EF.Functions.Like(t.ToBranch.NameAr, likeFilter)) ||
                    (t.Notes != null && EF.Functions.Like(t.Notes, likeFilter)) ||
                    (numericSearch.HasValue && t.Amount == numericSearch.Value));
            }

            if (filters.FromDate.HasValue)
            {
                var fromDate = filters.FromDate.Value.Date;
                query = query.Where(t => t.CreatedAt >= fromDate);
            }

            if (filters.ToDate.HasValue)
            {
                var toDateExclusive = filters.ToDate.Value.Date.AddDays(1);
                query = query.Where(t => t.CreatedAt < toDateExclusive);
            }

            return query;
        }

        private async Task<(Account? Account, string? ErrorMessage)> GetIntermediaryAccountAsync(int currencyId)
        {
            var currency = await _context.Currencies.FirstOrDefaultAsync(c => c.Id == currencyId);
            var currencyDisplay = currency?.Name ?? currency?.Code ?? currencyId.ToString();

            var candidateKeys = new List<string>();
            var seenKeys = new HashSet<string>(StringComparer.OrdinalIgnoreCase);

            void AddKey(string key)
            {
                if (seenKeys.Add(key))
                    candidateKeys.Add(key);
            }

            if (currency != null && !string.IsNullOrWhiteSpace(currency.Code))
            {
                var trimmedCode = currency.Code.Trim();
                if (!string.IsNullOrEmpty(trimmedCode))
                {
                    var lowerCode = trimmedCode.ToLowerInvariant();
                    AddKey($"{IntermediaryAccountSettingKeyPrefix}_{lowerCode}");

                    if (!string.Equals(trimmedCode, lowerCode, StringComparison.Ordinal))
                        AddKey($"{IntermediaryAccountSettingKeyPrefix}_{trimmedCode}");

                    var upperCode = trimmedCode.ToUpperInvariant();
                    if (!string.Equals(upperCode, lowerCode, StringComparison.Ordinal))
                        AddKey($"{IntermediaryAccountSettingKeyPrefix}_{upperCode}");
                }
            }

            AddKey($"{IntermediaryAccountSettingKeyPrefix}:{currencyId}");
            AddKey(IntermediaryAccountSettingKeyPrefix);

            SystemSetting? setting = null;
            foreach (var key in candidateKeys)
            {
                var candidate = await _context.SystemSettings.FirstOrDefaultAsync(s => s.Key == key);
                if (candidate != null && !string.IsNullOrWhiteSpace(candidate.Value))
                {
                    setting = candidate;
                    break;
                }
            }

            if (setting == null)
            {
                var expectedSuffix = currency != null && !string.IsNullOrWhiteSpace(currency.Code)
                    ? currency.Code.Trim().ToLowerInvariant()
                    : currencyId.ToString();

                return (null, $"لم يتم إعداد حساب الوسيط للحوالات لعملة {currencyDisplay}. الرجاء إعداد الإعداد {IntermediaryAccountSettingKeyPrefix}_{expectedSuffix}.");
            }

            if (!int.TryParse(setting.Value, out var accountId))
                return (null, "قيمة حساب الوسيط للحوالات غير صحيحة في الإعدادات.");

            var account = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == accountId);
            if (account == null)
                return (null, "حساب الوسيط المحدد في الإعدادات غير موجود.");

            if (account.CurrencyId != currencyId)
                return (null, $"حساب الوسيط المحدد لعملة {currencyDisplay} يجب أن يكون بنفس العملة.");

            return (account, null);
        }

        private async Task<bool> HasManagePermissionAsync()
        {
            var result = await _authorizationService.AuthorizeAsync(User, "transfers.manage");
            return result.Succeeded;
        }

        private string BuildSenderEntryDescription(User receiver, string? notes)
        {
            if (!string.IsNullOrWhiteSpace(notes))
                return notes!;

            var receiverName = !string.IsNullOrWhiteSpace(receiver.FullName)
                ? receiver.FullName
                : $"{receiver.FirstName} {receiver.LastName}".Trim();

            return string.IsNullOrWhiteSpace(receiverName) ? "حوالة صادرة" : $"حوالة صادرة إلى {receiverName}";
        }

        private string BuildReceiverEntryDescription(User? sender, string? notes)
        {
            if (!string.IsNullOrWhiteSpace(notes))
                return notes!;

            if (sender == null)
                return "حوالة مستلمة";

            var senderName = !string.IsNullOrWhiteSpace(sender.FullName)
                ? sender.FullName
                : $"{sender.FirstName} {sender.LastName}".Trim();

            return string.IsNullOrWhiteSpace(senderName) ? "حوالة مستلمة" : $"حوالة مستلمة من {senderName}";
        }

        private async Task<JournalEntry> CreateSenderJournalEntryAsync(PaymentTransfer transfer, Account intermediaryAccount, string createdById, string description)
        {
            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine
                {
                    AccountId = intermediaryAccount.Id,
                    DebitAmount = transfer.Amount,
                    Description = description
                },
                new JournalEntryLine
                {
                    AccountId = transfer.FromPaymentAccountId,
                    CreditAmount = transfer.Amount,
                    Description = description
                }
            };

            var entry = await _journalEntryService.CreateJournalEntryAsync(
                DateTime.Now,
                description,
                transfer.FromBranchId ?? transfer.ToBranchId ?? 0,
                createdById,
                lines,
                JournalEntryStatus.Posted);

            return entry;
        }

        private async Task<JournalEntry> CreateReceiverJournalEntryAsync(PaymentTransfer transfer, Account intermediaryAccount, string createdById, string description)
        {
            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine
                {
                    AccountId = transfer.ToPaymentAccountId,
                    DebitAmount = transfer.Amount,
                    Description = description
                },
                new JournalEntryLine
                {
                    AccountId = intermediaryAccount.Id,
                    CreditAmount = transfer.Amount,
                    Description = description
                }
            };

            var entry = await _journalEntryService.CreateJournalEntryAsync(
                DateTime.Now,
                description,
                transfer.ToBranchId ?? transfer.FromBranchId ?? 0,
                createdById,
                lines,
                JournalEntryStatus.Posted);

            return entry;
        }

        private async Task ReverseAccountBalances(JournalEntry entry)
        {
            foreach (var line in entry.Lines)
            {
                var account = await _context.Accounts.FindAsync(line.AccountId);
                if (account == null) continue;

                var netAmount = account.Nature == AccountNature.Debit
                    ? line.DebitAmount - line.CreditAmount
                    : line.CreditAmount - line.DebitAmount;

                account.CurrentBalance -= netAmount;
                account.UpdatedAt = DateTime.Now;
            }
        }

        private async Task UpdateSenderJournalEntryAsync(PaymentTransfer transfer, User receiver, decimal amount, string notes, int currencyId)
        {
            var (intermediaryAccount, intermediaryError) = await GetIntermediaryAccountAsync(currencyId);
            if (intermediaryAccount == null)
                throw new InvalidOperationException(intermediaryError ?? "لم يتم إعداد حساب الوسيط للحوالات.");

            JournalEntry? entry = null;
            if (transfer.SenderJournalEntryId.HasValue)
            {
                entry = await _context.JournalEntries
                    .Include(e => e.Lines)
                    .FirstOrDefaultAsync(e => e.Id == transfer.SenderJournalEntryId.Value);
            }

            var description = string.IsNullOrWhiteSpace(notes)
                ? BuildSenderEntryDescription(receiver, transfer.Notes)
                : notes;

            if (entry != null)
            {
                await ReverseAccountBalances(entry);
                _context.JournalEntryLines.RemoveRange(entry.Lines);
                _context.JournalEntries.Remove(entry);
                transfer.SenderJournalEntry = null;
                transfer.SenderJournalEntryId = null;
            }

            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine
                {
                    AccountId = intermediaryAccount.Id,
                    DebitAmount = amount,
                    Description = description
                },
                new JournalEntryLine
                {
                    AccountId = transfer.FromPaymentAccountId,
                    CreditAmount = amount,
                    Description = description
                }
            };

            var createdEntry = await _journalEntryService.CreateJournalEntryAsync(
                DateTime.Now,
                description,
                transfer.FromBranchId ?? transfer.ToBranchId ?? 0,
                transfer.SenderId,
                lines,
                JournalEntryStatus.Posted);

            transfer.SenderJournalEntry = createdEntry;
            transfer.SenderJournalEntryId = createdEntry.Id;
        }

        private IActionResult RedirectAfterAction(string? returnUrl)
        {
            if (!string.IsNullOrEmpty(returnUrl) && Url.IsLocalUrl(returnUrl))
                return Redirect(returnUrl);

            return RedirectToAction(nameof(Index));
        }

        private async Task<bool> CanAccessTransferAsync(PaymentTransfer transfer)
        {
            if (await HasManagePermissionAsync())
                return true;

            var userId = _userManager.GetUserId(User);
            return userId != null && (transfer.SenderId == userId || transfer.ReceiverId == userId);
        }

        private Dictionary<int, int> ParseBreakdown(string? json)
        {
            if (string.IsNullOrWhiteSpace(json))
                return new Dictionary<int, int>();

            var parsed = JsonSerializer.Deserialize<Dictionary<int, int>>(json);
            return parsed ?? new Dictionary<int, int>();
        }

        private async Task<Dictionary<int, string>> LoadCurrencyUnitNamesAsync(Dictionary<int, int> breakdown)
        {
            if (breakdown.Count == 0)
                return new Dictionary<int, string>();

            var unitIds = breakdown.Keys.ToList();
            return await _context.CurrencyUnits
                .Where(u => unitIds.Contains(u.Id))
                .OrderByDescending(u => u.ValueInBaseUnit)
                .ToDictionaryAsync(u => u.Id, u => $"{u.Name} ({u.ValueInBaseUnit:N2})");
        }
    }
}
