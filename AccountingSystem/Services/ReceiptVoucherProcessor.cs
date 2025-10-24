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
    public class ReceiptVoucherProcessor : IReceiptVoucherProcessor
    {
        private readonly ApplicationDbContext _context;
        private readonly IJournalEntryService _journalEntryService;

        public ReceiptVoucherProcessor(ApplicationDbContext context, IJournalEntryService journalEntryService)
        {
            _context = context;
            _journalEntryService = journalEntryService;
        }

        public async Task FinalizeAsync(ReceiptVoucher voucher, string approvedById, CancellationToken cancellationToken = default)
        {
            var loadedVoucher = await _context.ReceiptVouchers
                .Include(v => v.Account)
                .Include(v => v.PaymentAccount)
                .Include(v => v.CreatedBy)
                .FirstOrDefaultAsync(v => v.Id == voucher.Id, cancellationToken);

            if (loadedVoucher == null)
            {
                throw new InvalidOperationException($"Receipt voucher {voucher.Id} not found");
            }

            if (loadedVoucher.CreatedBy.PaymentBranchId == null)
            {
                throw new InvalidOperationException("Creator branch is required to finalize receipt voucher");
            }

            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine { AccountId = loadedVoucher.PaymentAccountId, DebitAmount = loadedVoucher.Amount },
                new JournalEntryLine { AccountId = loadedVoucher.AccountId, CreditAmount = loadedVoucher.Amount }
            };

            var reference = $"RCV:{loadedVoucher.Id}";
            var existingEntry = await _context.JournalEntries
                .FirstOrDefaultAsync(j => j.Reference == reference, cancellationToken);

            if (existingEntry == null)
            {
                await _journalEntryService.CreateJournalEntryAsync(
                    loadedVoucher.Date,
                    loadedVoucher.Notes ?? "سند قبض",
                    loadedVoucher.CreatedBy.PaymentBranchId.Value,
                    approvedById,
                    lines,
                    JournalEntryStatus.Posted,
                    reference: reference);
            }

            loadedVoucher.Status = ReceiptVoucherStatus.Approved;
            loadedVoucher.ApprovedAt = DateTime.Now;
            loadedVoucher.ApprovedById = approvedById;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
