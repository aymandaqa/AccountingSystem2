using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.ViewModels;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;
using System.Collections.Generic;
using System.Text.Json;

namespace AccountingSystem.Controllers
{
    [Authorize(Policy = "transfers.view")]
    public class TransfersController : Controller
    {
        private readonly ApplicationDbContext _context;
        private readonly UserManager<User> _userManager;

        public TransfersController(ApplicationDbContext context, UserManager<User> userManager)
        {
            _context = context;
            _userManager = userManager;
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
            ViewBag.CurrentUserId = user.Id;
            return View(transfers);
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
                .Include(u => u.Account)
                .FirstOrDefaultAsync(u => u.UserId == sender.Id && u.AccountId == model.FromPaymentAccountId.Value);
            if (senderAccount == null)
            {
                ModelState.AddModelError(nameof(model.FromPaymentAccountId), "الحساب المحدد غير متاح للمستخدم");
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

                if (units.Count != breakdownMap.Count || units.Any(u => senderAccount.Account == null || u.CurrencyId != senderAccount.Account.CurrencyId))
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

            if (senderAccount.Account != null && senderAccount.Account.Nature == AccountNature.Debit && finalAmount > senderAccount.Account.CurrentBalance)
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

            _context.PaymentTransfers.Add(transfer);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "transfers.approve")]
        public async Task<IActionResult> Approve(int id, bool accept)
        {
            var userId = _userManager.GetUserId(User);
            var transfer = await _context.PaymentTransfers
                .Include(t => t.FromBranch)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null || transfer.ReceiverId != userId || transfer.Status != TransferStatus.Pending)
                return NotFound();

            if (accept)
            {
                var fromAccount = await _context.Accounts.FindAsync(transfer.FromPaymentAccountId);
                if (fromAccount != null && fromAccount.Nature == AccountNature.Debit && transfer.Amount > fromAccount.CurrentBalance)
                {
                    TempData["ErrorMessage"] = "الرصيد المتاح في حساب المرسل لا يكفي لإتمام التحويل.";
                    return RedirectToAction(nameof(Index));
                }

                var number = await GenerateJournalEntryNumber();
                var entry = new JournalEntry
                {
                    Number = number,
                    Date = DateTime.Now,
                    Description = transfer.Notes ?? "تحويل",
                    BranchId = transfer.FromBranchId ?? transfer.ToBranchId ?? 0,
                    CreatedById = userId,
                    TotalDebit = transfer.Amount,
                    TotalCredit = transfer.Amount,
                    Status = JournalEntryStatus.Posted
                };
                entry.Lines.Add(new JournalEntryLine
                {
                    AccountId = transfer.ToPaymentAccountId,
                    DebitAmount = transfer.Amount,
                    Description = transfer.Notes ?? "تحويل",
                });
                entry.Lines.Add(new JournalEntryLine
                {
                    AccountId = transfer.FromPaymentAccountId,
                    CreditAmount = transfer.Amount,
                    Description = transfer.Notes ?? "تحويل",

                });

                _context.JournalEntries.Add(entry);
                await UpdateAccountBalances(entry);
                await _context.SaveChangesAsync();
                transfer.JournalEntryId = entry.Id;
            }

            transfer.Status = accept ? TransferStatus.Accepted : TransferStatus.Rejected;
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "transfers.create")]
        public async Task<IActionResult> Edit(int id)
        {
            var userId = _userManager.GetUserId(User);
            var transfer = await _context.PaymentTransfers
                .Include(t => t.FromBranch)
                .Include(t => t.ToBranch)
                .FirstOrDefaultAsync(t => t.Id == id);
            if (transfer == null || transfer.SenderId != userId || transfer.Status != TransferStatus.Pending)
                return NotFound();

            var model = new TransferEditViewModel
            {
                Id = transfer.Id,
                ReceiverId = transfer.ReceiverId,
                Amount = transfer.Amount,
                Notes = transfer.Notes
            };

            if (!await PopulateEditViewModelAsync(model, transfer))
                return NotFound();

            ViewBag.UserBranches = JsonSerializer.Serialize(model.ReceiverBranches);
            return View(model);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "transfers.create")]
        public async Task<IActionResult> Edit(TransferEditViewModel model)
        {
            var userId = _userManager.GetUserId(User);
            var transfer = await _context.PaymentTransfers
                .Include(t => t.FromBranch)
                .FirstOrDefaultAsync(t => t.Id == model.Id);
            if (transfer == null || transfer.SenderId != userId || transfer.Status != TransferStatus.Pending)
                return NotFound();

            if (!ModelState.IsValid)
            {
                if (!await PopulateEditViewModelAsync(model, transfer))
                    return NotFound();
                ViewBag.UserBranches = JsonSerializer.Serialize(model.ReceiverBranches);
                return View(model);
            }

            var receiver = await _context.Users.Include(u => u.PaymentBranch).FirstOrDefaultAsync(u => u.Id == model.ReceiverId);
            if (receiver == null || receiver.Id == userId)
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

            transfer.ReceiverId = receiver.Id;
            transfer.ToPaymentAccountId = receiverAccount.AccountId;
            transfer.ToBranchId = receiver.PaymentBranchId;
            transfer.Amount = finalAmount;
            transfer.Notes = model.Notes;
            transfer.CurrencyBreakdownJson = breakdownJson;

            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "transfers.create")]
        public async Task<IActionResult> Delete(int id)
        {
            var userId = _userManager.GetUserId(User);
            var transfer = await _context.PaymentTransfers.FindAsync(id);
            if (transfer == null || transfer.SenderId != userId || transfer.Status != TransferStatus.Pending)
                return NotFound();

            _context.PaymentTransfers.Remove(transfer);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
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

            return true;
        }

        private async Task UpdateAccountBalances(JournalEntry entry)
        {
            foreach (var line in entry.Lines)
            {
                var account = await _context.Accounts.FindAsync(line.AccountId);
                if (account == null) continue;

                var netAmount = account.Nature == AccountNature.Debit
                    ? line.DebitAmount - line.CreditAmount
                    : line.CreditAmount - line.DebitAmount;

                account.CurrentBalance += netAmount;
                account.UpdatedAt = DateTime.Now;
            }
        }

        private async Task<string> GenerateJournalEntryNumber()
        {
            var year = DateTime.Now.Year;
            var lastEntry = await _context.JournalEntries
                .Where(j => j.Date.Year == year)
                .OrderByDescending(j => j.Number)
                .FirstOrDefaultAsync();
            if (lastEntry == null)
                return $"JE{year}001";
            var lastNumber = lastEntry.Number.Substring(6);
            if (int.TryParse(lastNumber, out int number))
                return $"JE{year}{(number + 1):D3}";
            return $"JE{year}001";
        }
    }
}
