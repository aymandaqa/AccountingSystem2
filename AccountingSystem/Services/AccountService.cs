using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services
{
    public class AccountService : IAccountService
    {
        private readonly ApplicationDbContext _context;

        public AccountService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<(int Id, string Code)> CreateAccountAsync(string name, int parentAccountId)
        {
            // Check if account name already exists
            var existing = await _context.Accounts
                .FirstOrDefaultAsync(a => a.NameAr == name);

            if (existing != null)
            {
                return (existing.Id, existing.Code);
            }

            var parent = await _context.Accounts.FirstOrDefaultAsync(a => a.Id == parentAccountId);
            if (parent == null)
                throw new ArgumentException("Parent account not found", nameof(parentAccountId));

            var code = await GenerateNextCodeAsync(parent);

            var account = new Account
            {
                NameAr = name,
                NameEn = name,
                Code = code,
                ParentId = parent.Id,
                Level = parent.Level + 1,
                AccountType = parent.AccountType,
                Nature = parent.Nature,
                Classification = parent.Classification,
                SubClassification = parent.SubClassification,
                CurrencyId = parent.CurrencyId,
                CanHaveChildren = true,
                CanPostTransactions = true
            };

            _context.Accounts.Add(account);
            await _context.SaveChangesAsync();

            return (account.Id, account.Code);
        }

        private async Task<string> GenerateNextCodeAsync(Account parent)
        {
            var lastChildCode = await _context.Accounts
                .Where(a => a.ParentId == parent.Id)
                .OrderByDescending(a => a.Code)
                .Select(a => a.Code)
                .FirstOrDefaultAsync();

            int next = 1;
            if (!string.IsNullOrEmpty(lastChildCode) && lastChildCode.Length > parent.Code.Length)
            {
                var suffix = lastChildCode.Substring(parent.Code.Length);
                if (int.TryParse(suffix, out var num))
                    next = num + 1;
            }

            return parent.Code + next.ToString("D2");
        }
    }
}
