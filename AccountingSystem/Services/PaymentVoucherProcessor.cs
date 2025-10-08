using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.Workflows;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public class PaymentVoucherProcessor : IPaymentVoucherProcessor
    {
        private readonly ApplicationDbContext _context;
        private readonly IJournalEntryService _journalEntryService;

        public PaymentVoucherProcessor(ApplicationDbContext context, IJournalEntryService journalEntryService)
        {
            _context = context;
            _journalEntryService = journalEntryService;
        }

        public async Task FinalizeVoucherAsync(PaymentVoucher voucher, string approvedById, CancellationToken cancellationToken = default)
        {
            if (voucher.Status == PaymentVoucherStatus.Approved)
            {
                return;
            }

            var loadedVoucher = await _context.PaymentVouchers
                .Include(v => v.Supplier).ThenInclude(s => s.Account)
                .Include(v => v.CreatedBy)
                .FirstOrDefaultAsync(v => v.Id == voucher.Id, cancellationToken);

            if (loadedVoucher == null)
            {
                throw new InvalidOperationException($"Payment voucher {voucher.Id} not found");
            }

            if (loadedVoucher.Supplier.AccountId == null)
            {
                throw new InvalidOperationException("Supplier account is required to finalize voucher");
            }

            Account? selectedAccount = null;
            if (loadedVoucher.AccountId.HasValue)
            {
                selectedAccount = await _context.Accounts
                    .Include(a => a.Currency)
                    .FirstOrDefaultAsync(a => a.Id == loadedVoucher.AccountId.Value, cancellationToken);
            }

            if (selectedAccount == null)
            {
                throw new InvalidOperationException("Voucher account is required");
            }

            var supplierAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == loadedVoucher.Supplier.AccountId, cancellationToken);
            if (supplierAccount == null)
            {
                throw new InvalidOperationException("Supplier account not found");
            }

            Account? cashAccount = null;
            if (loadedVoucher.IsCash && loadedVoucher.CreatedBy.PaymentAccountId.HasValue)
            {
                cashAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == loadedVoucher.CreatedBy.PaymentAccountId.Value, cancellationToken);
                if (cashAccount == null)
                {
                    throw new InvalidOperationException("Cash account not found for creator");
                }
            }

            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine { AccountId = selectedAccount.Id, DebitAmount = loadedVoucher.Amount },
                new JournalEntryLine { AccountId = supplierAccount.Id, CreditAmount = loadedVoucher.Amount }
            };

            if (loadedVoucher.IsCash && cashAccount != null)
            {
                lines.Add(new JournalEntryLine { AccountId = supplierAccount.Id, DebitAmount = loadedVoucher.Amount });
                lines.Add(new JournalEntryLine { AccountId = cashAccount.Id, CreditAmount = loadedVoucher.Amount });
            }

            var reference = $"PAYV:{loadedVoucher.Id}";
            var existingEntry = await _context.JournalEntries.FirstOrDefaultAsync(j => j.Reference == reference, cancellationToken);
            if (existingEntry == null)
            {
                await _journalEntryService.CreateJournalEntryAsync(
                    loadedVoucher.Date,
                    loadedVoucher.Notes == null ? "سند دفع" : "سند دفع" + Environment.NewLine + loadedVoucher.Notes,
                    loadedVoucher.CreatedBy.PaymentBranchId ?? throw new InvalidOperationException("Creator branch is required"),
                    approvedById,
                    lines,
                    JournalEntryStatus.Posted,
                    reference: reference);
            }

            loadedVoucher.Status = PaymentVoucherStatus.Approved;
            loadedVoucher.ApprovedAt = DateTime.UtcNow;
            loadedVoucher.ApprovedById = approvedById;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
