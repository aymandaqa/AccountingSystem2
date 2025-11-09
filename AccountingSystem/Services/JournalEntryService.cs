using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Microsoft.EntityFrameworkCore.Storage;
using System;
using System.Collections.Generic;
using System.Data;
using System.Data.Common;
using System.Linq;
using System.Threading;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public class JournalEntryService : IJournalEntryService
    {
        private readonly ApplicationDbContext _context;
        private const string BalancingAccountSettingKey = "JournalEntryBalancingAccountId";
        private const string JournalEntryCounterKey = "JournalEntry";

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

            var currentTransaction = _context.Database.CurrentTransaction;

            if (currentTransaction != null)
            {
                return await CreateJournalEntryCoreAsync(
                    date,
                    description,
                    branchId,
                    createdById,
                    lineItems,
                    status,
                    reference,
                    number,
                    totalDebit,
                    totalCredit);
            }

            var executionStrategy = _context.Database.CreateExecutionStrategy();

            return await executionStrategy.ExecuteAsync(async () =>
            {
                await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable);

                try
                {
                    var entry = await CreateJournalEntryCoreAsync(
                        date,
                        description,
                        branchId,
                        createdById,
                        lineItems,
                        status,
                        reference,
                        number,
                        totalDebit,
                        totalCredit);

                    await transaction.CommitAsync();

                    return entry;
                }
                catch
                {
                    await transaction.RollbackAsync();
                    throw;
                }
            });
        }

        public async Task<string> GenerateJournalEntryNumber()
        {
            return await GenerateJournalEntryNumberCore();
        }

        private async Task<string> GenerateJournalEntryNumberCore(CancellationToken cancellationToken = default)
        {
            var year = System.DateTime.Now.Year;
            var prefix = $"JE{year}";

            var currentTransaction = _context.Database.CurrentTransaction;

            var sequenceValue = currentTransaction != null
                ? await GetNextCounterValueAsync(currentTransaction.GetDbTransaction(), JournalEntryCounterKey, year, cancellationToken)
                : await _context.Database.CreateExecutionStrategy().ExecuteAsync(async () =>
                {
                    await using var transaction = await _context.Database.BeginTransactionAsync(IsolationLevel.Serializable, cancellationToken);

                    try
                    {
                        var value = await GetNextCounterValueAsync(transaction.GetDbTransaction(), JournalEntryCounterKey, year, cancellationToken);
                        await transaction.CommitAsync(cancellationToken);
                        return value;
                    }
                    catch
                    {
                        await transaction.RollbackAsync(cancellationToken);
                        throw;
                    }
                });

            return $"{prefix}{sequenceValue:D9}";
        }

        private async Task<long> GetNextCounterValueAsync(DbTransaction? transaction, string key, int year, CancellationToken cancellationToken)
        {
            const string Sql = @"SET NOCOUNT ON;
DECLARE @output TABLE(Value bigint);

MERGE Counters WITH (HOLDLOCK) AS target
USING (SELECT @key AS [Key], @year AS [Year]) AS source
ON target.[Key] = source.[Key] AND target.[Year] = source.[Year]
WHEN MATCHED THEN
    UPDATE SET target.[Value] = target.[Value] + 1
WHEN NOT MATCHED THEN
    INSERT ([Key], [Year], [Value]) VALUES (source.[Key], source.[Year], 1)
OUTPUT inserted.[Value] INTO @output;

SELECT TOP (1) Value FROM @output;";

            var connection = transaction?.Connection ?? _context.Database.GetDbConnection();
            var shouldCloseConnection = connection.State != ConnectionState.Open;

            if (shouldCloseConnection)
            {
                await connection.OpenAsync(cancellationToken);
            }

            await using var command = connection.CreateCommand();
            command.CommandText = Sql;
            command.Transaction = transaction ?? _context.Database.CurrentTransaction?.GetDbTransaction();

            var keyParameter = command.CreateParameter();
            keyParameter.ParameterName = "@key";
            keyParameter.Value = key;
            command.Parameters.Add(keyParameter);

            var yearParameter = command.CreateParameter();
            yearParameter.ParameterName = "@year";
            yearParameter.Value = year;
            command.Parameters.Add(yearParameter);

            try
            {
                var result = await command.ExecuteScalarAsync(cancellationToken);
                return Convert.ToInt64(result);
            }
            finally
            {
                if (shouldCloseConnection && transaction == null)
                {
                    await connection.CloseAsync();
                }
            }
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

        private async Task<JournalEntry> CreateJournalEntryCoreAsync(
            System.DateTime date,
            string description,
            int branchId,
            string createdById,
            IReadOnlyCollection<JournalEntryLine> lineItems,
            JournalEntryStatus status,
            string? reference,
            string? number,
            decimal totalDebit,
            decimal totalCredit)
        {
            var entry = new JournalEntry
            {
                Number = string.IsNullOrWhiteSpace(number)
                    ? await GenerateJournalEntryNumberCore()
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
    }
}

