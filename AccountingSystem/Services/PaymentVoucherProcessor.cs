using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Models;
using AccountingSystem.Models.Workflows;
using Microsoft.EntityFrameworkCore;

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

        public async Task<JournalEntryPreview> BuildPreviewAsync(int voucherId, CancellationToken cancellationToken = default)
        {
            var loadedVoucher = await LoadVoucherAsync(voucherId, cancellationToken);
            return await BuildPreviewInternalAsync(loadedVoucher, cancellationToken);
        }

        public async Task FinalizeVoucherAsync(PaymentVoucher voucher, string approvedById, CancellationToken cancellationToken = default)
        {
            if (voucher == null)
            {
                throw new ArgumentNullException(nameof(voucher));
            }

            var loadedVoucher = await LoadVoucherAsync(voucher.Id, cancellationToken);
            var preview = await BuildPreviewInternalAsync(loadedVoucher, cancellationToken);

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

            loadedVoucher.Status = PaymentVoucherStatus.Approved;
            loadedVoucher.ApprovedAt = DateTime.Now;
            loadedVoucher.ApprovedById = approvedById;

            await _context.SaveChangesAsync(cancellationToken);
        }

        private async Task<PaymentVoucher> LoadVoucherAsync(int voucherId, CancellationToken cancellationToken)
        {
            var loadedVoucher = await _context.PaymentVouchers
                .Include(v => v.Supplier).ThenInclude(s => s.Account)
                .Include(v => v.Agent).ThenInclude(a => a.Account)
                .Include(v => v.CreatedBy)
                .FirstOrDefaultAsync(v => v.Id == voucherId, cancellationToken);

            if (loadedVoucher == null)
            {
                throw new InvalidOperationException($"Payment voucher {voucherId} not found");
            }

            return loadedVoucher;
        }

        private async Task<JournalEntryPreview> BuildPreviewInternalAsync(PaymentVoucher loadedVoucher, CancellationToken cancellationToken)
        {
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

            var branchId = loadedVoucher.CreatedBy.PaymentBranchId
                ?? throw new InvalidOperationException("Creator branch is required");

            var preview = new JournalEntryPreview
            {
                BranchId = branchId
            };

            if (loadedVoucher.SupplierId.HasValue)
            {
                if (loadedVoucher.Supplier?.AccountId == null)
                {
                    throw new InvalidOperationException("Supplier account is required to finalize voucher");
                }

                var supplierAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == loadedVoucher.Supplier.AccountId, cancellationToken)
                    ?? throw new InvalidOperationException("Supplier account not found");

                Account? cashAccount = null;
                if (loadedVoucher.IsCash && loadedVoucher.CreatedBy.PaymentAccountId.HasValue)
                {
                    cashAccount = await _context.Accounts
                        .FirstOrDefaultAsync(a => a.Id == loadedVoucher.CreatedBy.PaymentAccountId.Value, cancellationToken)
                        ?? throw new InvalidOperationException("Cash account not found for creator");

                    if (cashAccount.Nature == AccountNature.Debit && loadedVoucher.Amount > cashAccount.CurrentBalance)
                    {
                        throw new InvalidOperationException("الرصيد المتاح في حساب الدفع لا يكفي لإتمام العملية.");
                    }
                }

                preview.Lines.Add(new JournalEntryPreviewLine
                {
                    Account = selectedAccount,
                    Debit = loadedVoucher.Amount,
                    Description = BuildLineDescription("سند مصاريف", loadedVoucher.Notes)
                });

                preview.Lines.Add(new JournalEntryPreviewLine
                {
                    Account = supplierAccount,
                    Credit = loadedVoucher.Amount,
                    Description = BuildLineDescription("سند مصاريف", loadedVoucher.Notes)
                });

                if (loadedVoucher.IsCash && cashAccount != null)
                {
                    preview.Lines.Add(new JournalEntryPreviewLine
                    {
                        Account = supplierAccount,
                        Debit = loadedVoucher.Amount,
                        Description = BuildLineDescription("سند دفع مصاريف", loadedVoucher.Notes)
                    });

                    preview.Lines.Add(new JournalEntryPreviewLine
                    {
                        Account = cashAccount,
                        Credit = loadedVoucher.Amount,
                        Description = BuildLineDescription("سند دفع مصاريف", loadedVoucher.Notes)
                    });
                }

                preview.Reference = $"سند مصاريف:{loadedVoucher.Id}";
                preview.Description = BuildLineDescription("سند مصاريف", loadedVoucher.Notes);
            }
            else if (loadedVoucher.AgentId.HasValue)
            {
                if (loadedVoucher.Agent?.AccountId == null)
                {
                    throw new InvalidOperationException("Agent account is required to finalize voucher");
                }

                var agentAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == loadedVoucher.Agent.AccountId.Value, cancellationToken)
                    ?? throw new InvalidOperationException("Agent account not found");

                if (selectedAccount.CurrencyId != agentAccount.CurrencyId)
                {
                    throw new InvalidOperationException("عملة حساب الوكيل لا تطابق عملة حساب الدفع.");
                }

                if (selectedAccount.Nature == AccountNature.Debit && loadedVoucher.Amount > selectedAccount.CurrentBalance)
                {
                    throw new InvalidOperationException("الرصيد المتاح في حساب الدفع لا يكفي لإتمام العملية.");
                }

                preview.Lines.Add(new JournalEntryPreviewLine
                {
                    Account = agentAccount,
                    Debit = loadedVoucher.Amount,
                    Description = BuildLineDescription("سند دفع وكيل", loadedVoucher.Notes)
                });

                preview.Lines.Add(new JournalEntryPreviewLine
                {
                    Account = selectedAccount,
                    Credit = loadedVoucher.Amount,
                    Description = BuildLineDescription("سند دفع وكيل", loadedVoucher.Notes)
                });

                preview.Reference = $"سند دفع وكيل:{loadedVoucher.Id}";
                preview.Description = BuildLineDescription("سند دفع وكيل", loadedVoucher.Notes);
            }
            else
            {
                throw new InvalidOperationException("Voucher must target a supplier or an agent account");
            }

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
