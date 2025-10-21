using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Threading;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public class DisbursementVoucherProcessor : IDisbursementVoucherProcessor
    {
        private readonly ApplicationDbContext _context;
        private readonly IJournalEntryService _journalEntryService;

        public DisbursementVoucherProcessor(ApplicationDbContext context, IJournalEntryService journalEntryService)
        {
            _context = context;
            _journalEntryService = journalEntryService;
        }

        public async Task FinalizeAsync(DisbursementVoucher voucher, string approvedById, CancellationToken cancellationToken = default)
        {
            var loadedVoucher = await _context.DisbursementVouchers
                .Include(v => v.Supplier).ThenInclude(s => s.Account)
                .Include(v => v.CreatedBy)
                .FirstOrDefaultAsync(v => v.Id == voucher.Id, cancellationToken);

            if (loadedVoucher == null)
            {
                throw new InvalidOperationException($"Disbursement voucher {voucher.Id} not found");
            }

            if (loadedVoucher.CreatedBy.PaymentAccountId == null)
            {
                throw new InvalidOperationException("Creator payment account is required to finalize disbursement voucher");
            }

            if (loadedVoucher.CreatedBy.PaymentBranchId == null)
            {
                throw new InvalidOperationException("Creator branch is required to finalize disbursement voucher");
            }

            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine { AccountId = loadedVoucher.AccountId, DebitAmount = loadedVoucher.Amount },
                new JournalEntryLine { AccountId = loadedVoucher.CreatedBy.PaymentAccountId.Value, CreditAmount = loadedVoucher.Amount }
            };

            var reference = $"DSBV:{loadedVoucher.Id}";
            var existingEntry = await _context.JournalEntries
                .FirstOrDefaultAsync(j => j.Reference == reference, cancellationToken);

            if (existingEntry == null)
            {
                await _journalEntryService.CreateJournalEntryAsync(
                    loadedVoucher.Date,
                    loadedVoucher.Notes ?? "سند صرف",
                    loadedVoucher.CreatedBy.PaymentBranchId.Value,
                    approvedById,
                    lines,
                    JournalEntryStatus.Posted,
                    reference: reference);
            }

            loadedVoucher.Status = DisbursementVoucherStatus.Approved;
            loadedVoucher.ApprovedAt = DateTime.UtcNow;
            loadedVoucher.ApprovedById = approvedById;

            await _context.SaveChangesAsync(cancellationToken);
        }
    }
}
