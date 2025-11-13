using System;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;

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

        public async Task<JournalEntryPreview> BuildPreviewAsync(int voucherId, CancellationToken cancellationToken = default)
        {
            var loadedVoucher = await LoadVoucherAsync(voucherId, cancellationToken);
            return BuildPreviewInternal(loadedVoucher);
        }

        public async Task FinalizeAsync(DisbursementVoucher voucher, string approvedById, CancellationToken cancellationToken = default)
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

            loadedVoucher.Status = DisbursementVoucherStatus.Approved;
            loadedVoucher.ApprovedAt = DateTime.Now;
            loadedVoucher.ApprovedById = approvedById;

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<DisbursementVoucher> LoadVoucherAsync(int voucherId, CancellationToken cancellationToken)
        {
            var loadedVoucher = await _context.DisbursementVouchers
                .Include(v => v.Account)
                .Include(v => v.CreatedBy).ThenInclude(u => u.PaymentAccount)
                .FirstOrDefaultAsync(v => v.Id == voucherId, cancellationToken);

            if (loadedVoucher == null)
            {
                throw new InvalidOperationException($"Disbursement voucher {voucherId} not found");
            }

            if (loadedVoucher.CreatedBy.PaymentAccountId == null)
            {
                throw new InvalidOperationException("Creator payment account is required to finalize disbursement voucher");
            }

            if (loadedVoucher.CreatedBy.PaymentBranchId == null)
            {
                throw new InvalidOperationException("Creator branch is required to finalize disbursement voucher");
            }

            return loadedVoucher;
        }

        private JournalEntryPreview BuildPreviewInternal(DisbursementVoucher loadedVoucher)
        {
            var preview = new JournalEntryPreview
            {
                BranchId = loadedVoucher.CreatedBy.PaymentBranchId!.Value,
                Reference = $"DSBV:{loadedVoucher.Id}",
                Description = loadedVoucher.Notes ?? "سند صرف"
            };

            preview.Lines.Add(new JournalEntryPreviewLine
            {
                Account = loadedVoucher.Account,
                Debit = loadedVoucher.Amount,
                Description = "سند صرف"
            });

            var paymentAccount = loadedVoucher.CreatedBy.PaymentAccount
                ?? new Account { Id = loadedVoucher.CreatedBy.PaymentAccountId!.Value };

            preview.Lines.Add(new JournalEntryPreviewLine
            {
                Account = paymentAccount,
                Credit = loadedVoucher.Amount,
                Description = "سند صرف"
            });

            return preview;
        }
    }
}
