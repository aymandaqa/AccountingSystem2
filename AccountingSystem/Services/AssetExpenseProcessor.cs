using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.Extensions;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services
{
    public class AssetExpenseProcessor : IAssetExpenseProcessor
    {
        private readonly ApplicationDbContext _context;
        private readonly IJournalEntryService _journalEntryService;
        private readonly IAssetCostCenterService _assetCostCenterService;

        public AssetExpenseProcessor(ApplicationDbContext context, IJournalEntryService journalEntryService, IAssetCostCenterService assetCostCenterService)
        {
            _context = context;
            _journalEntryService = journalEntryService;
            _assetCostCenterService = assetCostCenterService;
        }

        public async Task<JournalEntryPreview> BuildPreviewAsync(int expenseId, CancellationToken cancellationToken = default)
        {
            var loadedExpense = await LoadExpenseAsync(expenseId, cancellationToken);
            return await BuildPreviewInternalAsync(loadedExpense, cancellationToken);
        }

        public async Task FinalizeAsync(AssetExpense expense, string approvedById, CancellationToken cancellationToken = default)
        {
            if (expense == null)
            {
                throw new ArgumentNullException(nameof(expense));
            }

            var loadedExpense = await LoadExpenseAsync(expense.Id, cancellationToken);

            if (loadedExpense.Asset == null)
            {
                throw new InvalidOperationException("الأصل غير موجود");
            }

            await _assetCostCenterService.EnsureCostCenterAsync(loadedExpense.Asset, cancellationToken);
            await _context.SaveChangesAsync(cancellationToken);

            // Refresh tracked values after ensuring cost center.
            await _context.Entry(loadedExpense.Asset).ReloadAsync(cancellationToken);
            await _context.Entry(loadedExpense.Asset).Reference(a => a.CostCenter).LoadAsync(cancellationToken);

            var preview = await BuildPreviewInternalAsync(loadedExpense, cancellationToken);

            var existingEntry = await _context.JournalEntries
                .FirstOrDefaultAsync(j => j.Reference == preview.Reference, cancellationToken);

            if (existingEntry != null)
            {
                return;
            }

            var lines = preview.Lines
                .Select(l => new JournalEntryLine
                {
                    AccountId = l.Account.Id,
                    DebitAmount = l.Debit,
                    CreditAmount = l.Credit,
                    Description = l.Description,
                    CostCenterId = l.CostCenter?.Id
                })
                .ToList();

            await _journalEntryService.CreateJournalEntryAsync(
                loadedExpense.Date,
                preview.Description,
                preview.BranchId,
                loadedExpense.CreatedById,
                lines,
                JournalEntryStatus.Posted,
                reference: preview.Reference,
                approvedById: approvedById);
        }

        private async Task<AssetExpense> LoadExpenseAsync(int expenseId, CancellationToken cancellationToken)
        {
            var loadedExpense = await _context.AssetExpenses
                .Include(e => e.Asset).ThenInclude(a => a.Branch)
                .Include(e => e.Asset).ThenInclude(a => a.CostCenter)
                .Include(e => e.ExpenseAccount)
                .Include(e => e.Supplier).ThenInclude(s => s.Account)
                .Include(e => e.CreatedBy)
                .FirstOrDefaultAsync(e => e.Id == expenseId, cancellationToken);

            if (loadedExpense == null)
            {
                throw new InvalidOperationException($"Asset expense {expenseId} not found");
            }

            return loadedExpense;
        }

        private async Task<JournalEntryPreview> BuildPreviewInternalAsync(AssetExpense loadedExpense, CancellationToken cancellationToken)
        {
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
                .FirstOrDefaultAsync(a => a.Id == loadedExpense.Supplier.AccountId, cancellationToken)
                ?? throw new InvalidOperationException("حساب المورد غير موجود");

            Account? paymentAccount = null;
            if (loadedExpense.IsCash)
            {
                if (!loadedExpense.AccountId.HasValue)
                {
                    throw new InvalidOperationException("لا يوجد حساب نقدي مرتبط بالمصروف");
                }

                paymentAccount = await _context.Accounts
                    .AsNoTracking()
                    .FirstOrDefaultAsync(a => a.Id == loadedExpense.AccountId.Value, cancellationToken)
                    ?? throw new InvalidOperationException("حساب الدفع غير موجود");

                if (paymentAccount.CurrencyId != loadedExpense.ExpenseAccount.CurrencyId)
                {
                    throw new InvalidOperationException("يجب أن تكون الحسابات بنفس العملة");
                }

                if (!paymentAccount.HasSufficientCashBalance(loadedExpense.Amount))
                {
                    throw new InvalidOperationException(AssetExpenseMessages.InsufficientPaymentBalanceMessage);
                }
            }

            var costCenter = loadedExpense.Asset?.CostCenterId.HasValue == true
                ? loadedExpense.Asset.CostCenter
                : null;

            var preview = new JournalEntryPreview
            {
                Reference = $"ASSETEXP:{loadedExpense.Id}",
                BranchId = loadedExpense.Asset?.BranchId
                    ?? throw new InvalidOperationException("لا يوجد فرع مرتبط بالأصل")
            };

            preview.Lines.Add(new JournalEntryPreviewLine
            {
                Account = loadedExpense.ExpenseAccount,
                Debit = loadedExpense.Amount,
                Description = BuildLineDescription("مصروف أصل", loadedExpense.Notes),
                CostCenter = costCenter
            });

            preview.Lines.Add(new JournalEntryPreviewLine
            {
                Account = supplierAccount,
                Credit = loadedExpense.Amount,
                Description = BuildLineDescription("مصروف أصل", loadedExpense.Notes)
            });

            if (loadedExpense.IsCash && paymentAccount != null)
            {
                preview.Lines.Add(new JournalEntryPreviewLine
                {
                    Account = supplierAccount,
                    Debit = loadedExpense.Amount,
                    Description = BuildLineDescription("دفع مصروف أصل", loadedExpense.Notes)
                });

                preview.Lines.Add(new JournalEntryPreviewLine
                {
                    Account = paymentAccount,
                    Credit = loadedExpense.Amount,
                    Description = BuildLineDescription("دفع مصروف أصل", loadedExpense.Notes)
                });
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

            preview.Description = string.Join(Environment.NewLine, descriptionLines);

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
