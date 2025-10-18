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
            var loadedVoucher = await _context.PaymentVouchers
                .Include(v => v.Supplier).ThenInclude(s => s.Account)
                .Include(v => v.Agent).ThenInclude(a => a.Account)
                .Include(v => v.CreatedBy)
                .FirstOrDefaultAsync(v => v.Id == voucher.Id, cancellationToken);

            if (loadedVoucher == null)
            {
                throw new InvalidOperationException($"Payment voucher {voucher.Id} not found");
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

            List<JournalEntryLine> lines;
            string reference;
            string description;

            if (loadedVoucher.SupplierId.HasValue)
            {
                if (loadedVoucher.Supplier?.AccountId == null)
                {
                    throw new InvalidOperationException("Supplier account is required to finalize voucher");
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

                    if (cashAccount.Nature == AccountNature.Debit && loadedVoucher.Amount > cashAccount.CurrentBalance)
                    {
                        throw new InvalidOperationException("الرصيد المتاح في حساب الدفع لا يكفي لإتمام العملية.");
                    }
                }

                lines = new List<JournalEntryLine>
                {
                    new JournalEntryLine { AccountId = selectedAccount.Id, DebitAmount = loadedVoucher.Amount, Description = "سند مصاريف" },
                    new JournalEntryLine { AccountId = supplierAccount.Id, CreditAmount = loadedVoucher.Amount, Description = "سند مصاريف" }
                };

                if (loadedVoucher.IsCash && cashAccount != null)
                {
                    lines.Add(new JournalEntryLine { AccountId = supplierAccount.Id, DebitAmount = loadedVoucher.Amount, Description = "سند دفع مصاريف" });
                    lines.Add(new JournalEntryLine { AccountId = cashAccount.Id, CreditAmount = loadedVoucher.Amount, Description = "سند دفع مصاريف" });
                }

                reference = $"سند مصاريف:{loadedVoucher.Id}";
                description = loadedVoucher.Notes == null ? "سند مصاريف" : "سند مصاريف" + Environment.NewLine + loadedVoucher.Notes;
            }
            else if (loadedVoucher.AgentId.HasValue)
            {
                if (loadedVoucher.Agent?.AccountId == null)
                {
                    throw new InvalidOperationException("Agent account is required to finalize voucher");
                }

                var agentAccount = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == loadedVoucher.Agent.AccountId.Value, cancellationToken);
                if (agentAccount == null)
                {
                    throw new InvalidOperationException("Agent account not found");
                }

                if (selectedAccount.CurrencyId != agentAccount.CurrencyId)
                {
                    throw new InvalidOperationException("عملة حساب الوكيل لا تطابق عملة حساب الدفع.");
                }

                lines = new List<JournalEntryLine>
                {
                    new JournalEntryLine { AccountId = selectedAccount.Id, DebitAmount = loadedVoucher.Amount, Description = "سند دفع وكيل" },
                    new JournalEntryLine { AccountId = agentAccount.Id, CreditAmount = loadedVoucher.Amount, Description = "سند دفع وكيل" }
                };

                reference = $"سند دفع وكيل:{loadedVoucher.Id}";
                description = loadedVoucher.Notes == null ? "سند دفع وكيل" : "سند دفع وكيل" + Environment.NewLine + loadedVoucher.Notes;
            }
            else
            {
                throw new InvalidOperationException("Voucher must target a supplier or an agent account");
            }

            var existingEntry = await _context.JournalEntries.FirstOrDefaultAsync(j => j.Reference == reference, cancellationToken);
            if (existingEntry == null)
            {
                await _journalEntryService.CreateJournalEntryAsync(
                    loadedVoucher.Date,
                    description,
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
