using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using System;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public class JournalEntryService : IJournalEntryService
    {
        private readonly ApplicationDbContext _context;
        private const string BalancingAccountSettingKey = "JournalEntryBalancingAccountId";

        public JournalEntryService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<JournalEntry> CreateJournalEntryAsync(
            System.DateTime date,
            string description,
            int branchId,
            string createdById,
            IEnumerable<JournalEntryLine> lines,
            JournalEntryStatus status,
            string? reference = null,
            string? number = null)
        {
            if (lines == null || !lines.Any())
                throw new System.ArgumentException("Entry must contain at least one line", nameof(lines));

            var lineItems = lines
                .Select(l => new JournalEntryLine
                {
                    AccountId = l.AccountId,
                    Description = string.IsNullOrWhiteSpace(l.Description) ? description : l.Description,
                    Reference = l.Reference,
                    DebitAmount = l.DebitAmount,
                    CreditAmount = l.CreditAmount,
                    CostCenterId = l.CostCenterId
                })
                .ToList();

            var accountIds = lineItems.Select(l => l.AccountId).Distinct().ToList();
            var currencyIds = await _context.Accounts
                .Where(a => accountIds.Contains(a.Id))
                .Select(a => a.CurrencyId)
                .Distinct()
                .ToListAsync();
            if (currencyIds.Count > 1)
                throw new System.ArgumentException("All accounts must have the same currency", nameof(lines));

            var totalDebit = lineItems.Sum(l => l.DebitAmount);
            var totalCredit = lineItems.Sum(l => l.CreditAmount);
            var difference = Math.Round(totalDebit - totalCredit, 2, MidpointRounding.AwayFromZero);

            if (difference != 0)
            {
                var setting = await _context.SystemSettings
                    .FirstOrDefaultAsync(s => s.Key == BalancingAccountSettingKey);

                if (setting == null || string.IsNullOrWhiteSpace(setting.Value) ||
                    !int.TryParse(setting.Value, out var balancingAccountId))
                {
                    throw new InvalidOperationException("لم يتم إعداد حساب موازنة القيود في الإعدادات");
                }

                var balancingAccount = await _context.Accounts
                    .FirstOrDefaultAsync(a => a.Id == balancingAccountId);

                if (balancingAccount == null)
                {
                    throw new InvalidOperationException("حساب موازنة القيود المحدد غير موجود");
                }

                if (currencyIds.Count == 1 && balancingAccount.CurrencyId != currencyIds[0])
                {
                    throw new InvalidOperationException("عملة حساب الموازنة لا تطابق عملة القيود");
                }

                var balancingLine = new JournalEntryLine
                {
                    AccountId = balancingAccount.Id,
                    Description = string.IsNullOrWhiteSpace(description)
                        ? "قيد موازنة تلقائي"
                        : description,
                    DebitAmount = difference < 0 ? Math.Abs(difference) : 0,
                    CreditAmount = difference > 0 ? difference : 0
                };

                lineItems.Add(balancingLine);
                totalDebit = lineItems.Sum(l => l.DebitAmount);
                totalCredit = lineItems.Sum(l => l.CreditAmount);
            }

            var entry = new JournalEntry
            {
                Number = string.IsNullOrWhiteSpace(number)
                    ? await GenerateJournalEntryNumber()
                    : number!,
                Date = date,
                Description = description,
                Reference = reference,
                BranchId = branchId,
                CreatedById = createdById,
                TotalDebit = totalDebit,
                TotalCredit = totalCredit,
                Status = status
            };

            foreach (var line in lineItems)
            {
                entry.Lines.Add(new JournalEntryLine
                {
                    AccountId = line.AccountId,
                    Description = string.IsNullOrWhiteSpace(line.Description) ? entry.Description : line.Description,
                    Reference = line.Reference,
                    DebitAmount = line.DebitAmount,
                    CreditAmount = line.CreditAmount,
                    CostCenterId = line.CostCenterId
                });
            }

            _context.JournalEntries.Add(entry);

            if (status == JournalEntryStatus.Posted)
            {
                await UpdateAccountBalances(entry);
            }

            await _context.SaveChangesAsync();
            return entry;
        }

        public async Task<string> GenerateJournalEntryNumber()
        {
            var year = System.DateTime.Now.Year;
            var prefix = $"JE{year}";

            var existingNumbers = await _context.JournalEntries
                .Where(j => j.Number.StartsWith(prefix))
                .Select(j => j.Number)
                .ToListAsync();

            if (existingNumbers.Count == 0)
            {
                return $"{prefix}001";
            }

            var maxSequence = existingNumbers
                .Select(n => n.Length > prefix.Length && int.TryParse(n.Substring(prefix.Length), out var seq) ? seq : 0)
                .DefaultIfEmpty(0)
                .Max();

            string candidate;
            do
            {
                maxSequence++;
                candidate = $"{prefix}{maxSequence:D3}";
            }
            while (await _context.JournalEntries.AnyAsync(j => j.Number == candidate));

            return candidate;
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
                account.UpdatedAt = System.DateTime.Now;
            }
        }
    }
}

