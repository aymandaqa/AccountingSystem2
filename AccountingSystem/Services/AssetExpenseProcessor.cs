using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public class AssetExpenseProcessor : IAssetExpenseProcessor
    {
        private readonly ApplicationDbContext _context;
        private readonly IJournalEntryService _journalEntryService;

        public AssetExpenseProcessor(ApplicationDbContext context, IJournalEntryService journalEntryService)
        {
            _context = context;
            _journalEntryService = journalEntryService;
        }

        public async Task FinalizeAsync(AssetExpense expense, string approvedById, CancellationToken cancellationToken = default)
        {
            var loadedExpense = await _context.AssetExpenses
                .Include(e => e.Asset).ThenInclude(a => a.Branch)
                .Include(e => e.ExpenseAccount)
                .Include(e => e.Supplier).ThenInclude(s => s.Account)
                .Include(e => e.CreatedBy)
                .FirstOrDefaultAsync(e => e.Id == expense.Id, cancellationToken);

            if (loadedExpense == null)
            {
                throw new InvalidOperationException($"Asset expense {expense.Id} not found");
            }

            if (loadedExpense.Supplier?.AccountId == null)
            {
                throw new InvalidOperationException("المورد غير مرتبط بحساب محاسبي");
            }

            if (loadedExpense.ExpenseAccount == null)
            {
                loadedExpense.ExpenseAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == loadedExpense.ExpenseAccountId, cancellationToken)
                    ?? throw new InvalidOperationException("حساب المصروف غير موجود");
            }

            var supplierAccount = await _context.Accounts
                .Include(a => a.Currency)
                .FirstOrDefaultAsync(a => a.Id == loadedExpense.Supplier.AccountId, cancellationToken);

            if (supplierAccount == null)
            {
                throw new InvalidOperationException("حساب المورد غير موجود");
            }

            var lines = new List<JournalEntryLine>
            {
                new JournalEntryLine
                {
                    AccountId = loadedExpense.ExpenseAccountId,
                    DebitAmount = loadedExpense.Amount,
                    Description = "مصروف أصل"
                },
                new JournalEntryLine
                {
                    AccountId = supplierAccount.Id,
                    CreditAmount = loadedExpense.Amount,
                    Description = "مصروف أصل"
                }
            };

            if (loadedExpense.IsCash)
            {
                if (!loadedExpense.AccountId.HasValue)
                {
                    throw new InvalidOperationException("لا يوجد حساب نقدي مرتبط بالمصروف");
                }

                var paymentAccount = await _context.Accounts
                    .Include(a => a.Currency)
                    .FirstOrDefaultAsync(a => a.Id == loadedExpense.AccountId.Value, cancellationToken);

                if (paymentAccount == null)
                {
                    throw new InvalidOperationException("حساب الدفع غير موجود");
                }

                if (paymentAccount.CurrencyId != loadedExpense.ExpenseAccount.CurrencyId)
                {
                    throw new InvalidOperationException("يجب أن تكون الحسابات بنفس العملة");
                }

                if (paymentAccount.Nature == AccountNature.Debit && loadedExpense.Amount > paymentAccount.CurrentBalance)
                {
                    throw new InvalidOperationException("الرصيد المتاح في حساب الدفع لا يكفي لإتمام العملية.");
                }

                lines.Add(new JournalEntryLine
                {
                    AccountId = supplierAccount.Id,
                    DebitAmount = loadedExpense.Amount,
                    Description = "دفع مصروف أصل"
                });

                lines.Add(new JournalEntryLine
                {
                    AccountId = paymentAccount.Id,
                    CreditAmount = loadedExpense.Amount,
                    Description = "دفع مصروف أصل"
                });
            }

            var reference = $"ASSETEXP:{loadedExpense.Id}";
            var existingEntry = await _context.JournalEntries
                .FirstOrDefaultAsync(j => j.Reference == reference, cancellationToken);

            if (existingEntry != null)
            {
                return;
            }

            var descriptionLines = new List<string> { "مصروف أصل" };
            if (!string.IsNullOrWhiteSpace(loadedExpense.Asset?.Name))
            {
                descriptionLines.Add(loadedExpense.Asset.Name);
            }

            if (!string.IsNullOrWhiteSpace(loadedExpense.Notes))
            {
                descriptionLines.Add(loadedExpense.Notes!);
            }

            var description = string.Join(Environment.NewLine, descriptionLines);

            if (loadedExpense.Asset?.BranchId == null)
            {
                throw new InvalidOperationException("لا يوجد فرع مرتبط بالأصل");
            }

            await _journalEntryService.CreateJournalEntryAsync(
                loadedExpense.Date,
                description,
                loadedExpense.Asset.BranchId,
                approvedById,
                lines,
                JournalEntryStatus.Posted,
                reference: reference);
        }
    }
}
