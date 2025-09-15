using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public class JournalEntryService : IJournalEntryService
    {
        private readonly ApplicationDbContext _context;

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
            string? reference = null)
        {
            if (lines == null || !lines.Any())
                throw new System.ArgumentException("Entry must contain at least one line", nameof(lines));

            var accountIds = lines.Select(l => l.AccountId).Distinct().ToList();
            var currencies = await _context.Accounts
                .Where(a => accountIds.Contains(a.Id))
                .Select(a => a.CurrencyId)
                .Distinct()
                .ToListAsync();
            if (currencies.Count > 1)
                throw new System.ArgumentException("All accounts must have the same currency", nameof(lines));

            var entry = new JournalEntry
            {
                Number = await GenerateJournalEntryNumber(),
                Date = date,
                Description = description,
                Reference = reference,
                BranchId = branchId,
                CreatedById = createdById,
                TotalDebit = lines.Sum(l => l.DebitAmount),
                TotalCredit = lines.Sum(l => l.CreditAmount),
                Status = status
            };

            foreach (var line in lines)
            {
                entry.Lines.Add(new JournalEntryLine
                {
                    AccountId = line.AccountId,
                    Description = string.IsNullOrWhiteSpace(line.Description) ? entry.Description : line.Description,
                    DebitAmount = line.DebitAmount,
                    CreditAmount = line.CreditAmount
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

        private async Task<string> GenerateJournalEntryNumber()
        {
            var year = System.DateTime.Now.Year;
            var lastEntry = await _context.JournalEntries
                .Where(j => j.Date.Year == year)
                .OrderByDescending(j => j.Number)
                .FirstOrDefaultAsync();

            if (lastEntry == null)
                return $"JE{year}001";

            var lastNumber = lastEntry.Number.Substring(6);
            if (int.TryParse(lastNumber, out int number))
                return $"JE{year}{(number + 1):D3}";

            return $"JE{year}001";
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

