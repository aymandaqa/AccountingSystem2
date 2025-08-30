using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Identity;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using System;
using System.Linq;

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
            var userId = _userManager.GetUserId(User);
            var transfers = await _context.PaymentTransfers
                .Include(t => t.Sender)
                .Include(t => t.Receiver)
                .Where(t => t.SenderId == userId || t.ReceiverId == userId)
                .OrderByDescending(t => t.CreatedAt)
                .ToListAsync();
            ViewBag.CurrentUserId = userId;
            return View(transfers);
        }

        [Authorize(Policy = "transfers.create")]
        public IActionResult Create()
        {
            ViewData["Users"] = new SelectList(_context.Users.ToList(), "Id", "FullName");
            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "transfers.create")]
        public async Task<IActionResult> Create(string receiverId, decimal amount, string? notes)
        {
            var sender = await _userManager.GetUserAsync(User);
            if (sender == null)
                return Challenge();

            var receiver = await _context.Users.FindAsync(receiverId);
            if (receiver == null)
            {
                ModelState.AddModelError("receiverId", "المستلم غير موجود");
                ViewData["Users"] = new SelectList(_context.Users.ToList(), "Id", "FullName");
                return View();
            }

            var transfer = new PaymentTransfer
            {
                SenderId = sender.Id,
                ReceiverId = receiver.Id,
                FromPaymentAccountId = sender.PaymentAccountId ?? 0,
                ToPaymentAccountId = receiver.PaymentAccountId ?? 0,
                FromBranchId = sender.PaymentBranchId,
                ToBranchId = receiver.PaymentBranchId,
                Amount = amount,
                Notes = notes,
                Status = TransferStatus.Pending,
                CreatedAt = DateTime.UtcNow
            };

            _context.PaymentTransfers.Add(transfer);
            await _context.SaveChangesAsync();
            return RedirectToAction(nameof(Index));
        }

        [Authorize(Policy = "transfers.approve")]
        public async Task<IActionResult> Approve(int id, bool accept)
        {
            var userId = _userManager.GetUserId(User);
            var transfer = await _context.PaymentTransfers.FindAsync(id);
            if (transfer == null || transfer.ReceiverId != userId || transfer.Status != TransferStatus.Pending)
                return NotFound();

            if (accept)
            {
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
            var transfer = await _context.PaymentTransfers.FindAsync(id);
            if (transfer == null || transfer.SenderId != userId || transfer.Status != TransferStatus.Pending)
                return NotFound();

            ViewData["Users"] = new SelectList(_context.Users.ToList(), "Id", "FullName", transfer.ReceiverId);
            return View(transfer);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        [Authorize(Policy = "transfers.create")]
        public async Task<IActionResult> Edit(int id, string receiverId, decimal amount, string? notes)
        {
            var userId = _userManager.GetUserId(User);
            var transfer = await _context.PaymentTransfers.FindAsync(id);
            if (transfer == null || transfer.SenderId != userId || transfer.Status != TransferStatus.Pending)
                return NotFound();

            var receiver = await _context.Users.FindAsync(receiverId);
            if (receiver == null)
            {
                ModelState.AddModelError("receiverId", "المستلم غير موجود");
                ViewData["Users"] = new SelectList(_context.Users.ToList(), "Id", "FullName", receiverId);
                return View(transfer);
            }

            transfer.ReceiverId = receiver.Id;
            transfer.ToPaymentAccountId = receiver.PaymentAccountId ?? 0;
            transfer.ToBranchId = receiver.PaymentBranchId;
            transfer.Amount = amount;
            transfer.Notes = notes;

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
                account.UpdatedAt = DateTime.UtcNow;
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
