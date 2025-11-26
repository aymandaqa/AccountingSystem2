using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.Workflows;
using Microsoft.EntityFrameworkCore;

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

        public async Task<JournalEntryPreview> BuildPreviewAsync(int voucherId, CancellationToken cancellationToken = default)
        {
            var loadedVoucher = await LoadVoucherAsync(voucherId, cancellationToken);
            return BuildPreviewInternal(loadedVoucher);
        }

        public async Task FinalizeAsync(ReceiptVoucher voucher, string approvedById, CancellationToken cancellationToken = default)
        {
            if (voucher == null)
            {
                throw new ArgumentNullException(nameof(voucher));
            }

            var loadedVoucher = await LoadVoucherAsync(voucher.Id, cancellationToken);
            var preview = BuildPreviewInternal(loadedVoucher);

            var existingEntry = await _context.JournalEntries
                .FirstOrDefaultAsync(j => j.Reference == preview.Reference, cancellationToken);

            if (existingEntry == null)
            {
                var lines = preview.Lines
                    .Select(l => new JournalEntryLine
                    {
                        AccountId = l.Account.Id,
                        DebitAmount = l.Debit,
                        CreditAmount = l.Credit,
                        Description = l.Description
                    })
                    .ToList();

                await _journalEntryService.CreateJournalEntryAsync(
                    loadedVoucher.Date,
                    preview.Description,
                    preview.BranchId,
                    loadedVoucher.CreatedById,
                    lines,
                    JournalEntryStatus.Posted,
                    reference: preview.Reference,
                    approvedById: approvedById);
            }

            loadedVoucher.Status = ReceiptVoucherStatus.Approved;
            loadedVoucher.ApprovedAt = DateTime.Now;
            loadedVoucher.ApprovedById = approvedById;

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<ReceiptVoucher> LoadVoucherAsync(int voucherId, CancellationToken cancellationToken)
        {
            var loadedVoucher = await _context.ReceiptVouchers
                .Include(v => v.Account)
                .Include(v => v.PaymentAccount)
                .Include(v => v.CreatedBy)
                .FirstOrDefaultAsync(v => v.Id == voucherId, cancellationToken);

            if (loadedVoucher == null)
            {
                throw new InvalidOperationException($"Receipt voucher {voucherId} not found");
            }

            if (loadedVoucher.CreatedBy.PaymentBranchId == null)
            {
                throw new InvalidOperationException("Creator branch is required to finalize receipt voucher");
            }

            return loadedVoucher;
        }

        private JournalEntryPreview BuildPreviewInternal(ReceiptVoucher loadedVoucher)
        {
            var preview = new JournalEntryPreview
            {
                BranchId = loadedVoucher.CreatedBy.PaymentBranchId!.Value,
                Reference = $"RCV:{loadedVoucher.Id}",
                Description = loadedVoucher.Notes ?? "سند قبض"
            };

            preview.Lines.Add(new JournalEntryPreviewLine
            {
                Account = loadedVoucher.PaymentAccount,
                Debit = loadedVoucher.Amount,
                Description = BuildLineDescription("سند قبض", loadedVoucher.Notes)
            });

            preview.Lines.Add(new JournalEntryPreviewLine
            {
                Account = loadedVoucher.Account,
                Credit = loadedVoucher.Amount,
                Description = BuildLineDescription("سند قبض", loadedVoucher.Notes)
            });

            return preview;
        }

        private static string BuildLineDescription(string baseDescription, string? notes)
        {
            return string.IsNullOrWhiteSpace(notes)
                ? baseDescription
                : baseDescription + Environment.NewLine + notes;
        }
    }
}
