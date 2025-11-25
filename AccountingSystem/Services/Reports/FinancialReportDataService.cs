using System;
using System.Collections;
using System.Collections.Generic;
using System.Linq;
using System.Threading.Tasks;
using AccountingSystem.Data;
using AccountingSystem.ViewModels.Reports;
using Microsoft.EntityFrameworkCore;

namespace AccountingSystem.Services.Reports
{
    public interface IFinancialReportDataService
    {
        Task<Dictionary<string, IEnumerable>> GetDataSourcesAsync(string reportKey, IDictionary<string, string?> parameters);
    }

    public class FinancialReportDataService : IFinancialReportDataService
    {
        private readonly ApplicationDbContext _context;

        public FinancialReportDataService(ApplicationDbContext context)
        {
            _context = context;
        }

        public async Task<Dictionary<string, IEnumerable>> GetDataSourcesAsync(string reportKey, IDictionary<string, string?> parameters)
        {
            return reportKey switch
            {
                "VoucherActivity" => await BuildVoucherActivityDataAsync(parameters),
                _ => await BuildJournalEntryLinesDataAsync(parameters)
            };
        }

        private async Task<Dictionary<string, IEnumerable>> BuildJournalEntryLinesDataAsync(IDictionary<string, string?> parameters)
        {
            var query = _context.JournalEntryLines
                .Include(l => l.JournalEntry)
                .ThenInclude(j => j.Branch)
                .Include(l => l.Account)
                .AsQueryable();

            if (TryParseDate(parameters, "fromDate", out var fromDate))
            {
                query = query.Where(l => l.JournalEntry!.EntryDate >= fromDate);
            }

            if (TryParseDate(parameters, "toDate", out var toDate))
            {
                query = query.Where(l => l.JournalEntry!.EntryDate <= toDate);
            }

            if (TryParseInt(parameters, "branchId", out var branchId))
            {
                query = query.Where(l => l.JournalEntry!.BranchId == branchId);
            }

            if (TryParseInt(parameters, "accountId", out var accountId))
            {
                query = query.Where(l => l.AccountId == accountId);
            }

            var data = await query
                .OrderByDescending(l => l.JournalEntry!.EntryDate)
                .Select(l => new JournalEntryLineReportRow
                {
                    BranchName = l.JournalEntry!.Branch != null ? l.JournalEntry.Branch.NameAr : string.Empty,
                    AccountNumber = l.Account!.Number,
                    AccountName = l.Account.NameAr ?? l.Account.NameEn ?? string.Empty,
                    EntryDate = l.JournalEntry.EntryDate,
                    Reference = l.JournalEntry.Reference ?? string.Empty,
                    Description = l.Description ?? string.Empty,
                    Debit = l.Debit,
                    Credit = l.Credit
                })
                .ToListAsync();

            return new Dictionary<string, IEnumerable>
            {
                { "JournalEntryLinesDataSet", data }
            };
        }

        private async Task<Dictionary<string, IEnumerable>> BuildVoucherActivityDataAsync(IDictionary<string, string?> parameters)
        {
            if (!TryParseDate(parameters, "fromDate", out var fromDate))
            {
                fromDate = DateTime.Today.AddMonths(-1);
            }

            if (!TryParseDate(parameters, "toDate", out var toDate))
            {
                toDate = DateTime.Today;
            }

            TryParseInt(parameters, "currencyId", out var currencyId);

            var receiptVouchers = _context.ReceiptVouchers
                .Include(v => v.Account)
                .Include(v => v.PaymentAccount)
                .Include(v => v.Currency)
                .Where(v => v.Date >= fromDate && v.Date <= toDate);

            var paymentVouchers = _context.PaymentVouchers
                .Include(v => v.Account)
                .Include(v => v.PaymentAccount)
                .Include(v => v.Currency)
                .Where(v => v.Date >= fromDate && v.Date <= toDate);

            var disbursementVouchers = _context.DisbursementVouchers
                .Include(v => v.Account)
                .Include(v => v.Currency)
                .Include(v => v.Supplier)
                .Where(v => v.Date >= fromDate && v.Date <= toDate);

            if (currencyId.HasValue)
            {
                receiptVouchers = receiptVouchers.Where(v => v.CurrencyId == currencyId.Value);
                paymentVouchers = paymentVouchers.Where(v => v.CurrencyId == currencyId.Value);
                disbursementVouchers = disbursementVouchers.Where(v => v.CurrencyId == currencyId.Value);
            }

            var receipts = await receiptVouchers
                .Select(v => new VoucherActivityRow
                {
                    VoucherType = "سند قبض",
                    Date = v.Date,
                    DebitAccount = v.Account.NameAr ?? v.Account.NameEn ?? string.Empty,
                    CreditAccount = v.PaymentAccount.NameAr ?? v.PaymentAccount.NameEn ?? string.Empty,
                    Amount = v.Amount,
                    Currency = v.Currency.Code,
                    Notes = v.Notes ?? string.Empty
                })
                .ToListAsync();

            var payments = await paymentVouchers
                .Select(v => new VoucherActivityRow
                {
                    VoucherType = "سند دفع",
                    Date = v.Date,
                    DebitAccount = v.PaymentAccount.NameAr ?? v.PaymentAccount.NameEn ?? string.Empty,
                    CreditAccount = v.Account.NameAr ?? v.Account.NameEn ?? string.Empty,
                    Amount = v.Amount,
                    Currency = v.Currency.Code,
                    Notes = v.Notes ?? string.Empty
                })
                .ToListAsync();

            var disbursements = await disbursementVouchers
                .Select(v => new VoucherActivityRow
                {
                    VoucherType = "سند صرف",
                    Date = v.Date,
                    DebitAccount = v.Account.NameAr ?? v.Account.NameEn ?? string.Empty,
                    CreditAccount = v.Supplier != null ? v.Supplier.NameAr ?? v.Supplier.NameEn ?? string.Empty : string.Empty,
                    Amount = v.Amount,
                    Currency = v.Currency.Code,
                    Notes = v.Notes ?? string.Empty
                })
                .ToListAsync();

            var data = receipts
                .Concat(payments)
                .Concat(disbursements)
                .OrderByDescending(v => v.Date)
                .ToList();

            return new Dictionary<string, IEnumerable>
            {
                { "VoucherActivityDataSet", data }
            };
        }

        private static bool TryParseDate(IDictionary<string, string?> parameters, string key, out DateTime value)
        {
            value = default;
            return parameters.TryGetValue(key, out var raw) && DateTime.TryParse(raw, out value);
        }

        private static bool TryParseInt(IDictionary<string, string?> parameters, string key, out int? value)
        {
            value = null;
            if (parameters.TryGetValue(key, out var raw) && int.TryParse(raw, out var parsed))
            {
                value = parsed;
                return true;
            }

            return false;
        }
    }

    public class JournalEntryLineReportRow
    {
        public string BranchName { get; set; } = string.Empty;
        public string AccountNumber { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public DateTime EntryDate { get; set; }
        public string Reference { get; set; } = string.Empty;
        public string Description { get; set; } = string.Empty;
        public decimal Debit { get; set; }
        public decimal Credit { get; set; }
    }

    public class VoucherActivityRow
    {
        public string VoucherType { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string DebitAccount { get; set; } = string.Empty;
        public string CreditAccount { get; set; } = string.Empty;
        public decimal Amount { get; set; }
        public string Currency { get; set; } = string.Empty;
        public string Notes { get; set; } = string.Empty;
    }
}
