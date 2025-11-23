using AccountingSystem.Data;
using AccountingSystem.Models;
using Microsoft.EntityFrameworkCore;
using Roadfn.Models;

namespace AccountingSystem.Services
{
    public class BusinessPaymentReconciliationService
    {
        private readonly RoadFnDbContext _roadContext;
        private readonly ApplicationDbContext _accountingContext;

        public BusinessPaymentReconciliationService(RoadFnDbContext roadContext, ApplicationDbContext accountingContext)
        {
            _roadContext = roadContext;
            _accountingContext = accountingContext;
        }

        public async Task<int> FixBusinessPaymentEntriesAsync(DateTime startDate)
        {
            var candidates = await (from w in _roadContext.RPTPaymentHistoryUsers
                                    join h in _roadContext.BisnessUserPaymentHeader on w.Id equals h.Id
                                    join d in _roadContext.BisnessUserPaymentDetails on w.Id equals d.HeaderId
                                    join s in _roadContext.Shipments on d.ShipmentId equals s.Id
                                    where h.PaymentDate >= startDate
                                    select new
                                    {
                                        PaymentId = w.Id,
                                        s.ShipmentTrackingNo,
                                        ShPrice = (s.ShipmentTotal ?? 0) - (s.ShipmentFees ?? 0) - (s.ShipmentExtraFees ?? 0)
                                    })
                .ToListAsync();

            var updatedLines = 0;

            foreach (var candidate in candidates)
            {
                var targetLines = await _accountingContext.JournalEntryLines
                    .Include(l => l.Account)
                    .Include(l => l.JournalEntry)
                    .Where(l => l.Reference == candidate.PaymentId.ToString()
                        && l.Description != null
                        && l.Description.Contains(candidate.ShipmentTrackingNo ?? string.Empty)
                        && l.Description.Contains("دفع ذمة مورد")
                        && l.Account.Code.StartsWith("210101"))
                    .ToListAsync();

                foreach (var line in targetLines)
                {
                    var desiredDebit = candidate.ShPrice < 0 ? Math.Abs(candidate.ShPrice) : 0m;
                    var desiredCredit = candidate.ShPrice > 0 ? candidate.ShPrice : 0m;

                    var needsUpdate = line.DebitAmount != desiredDebit || line.CreditAmount != desiredCredit;

                    if (needsUpdate)
                    {
                        line.DebitAmount = desiredDebit;
                        line.CreditAmount = desiredCredit;
                        updatedLines++;
                    }

                    var counterpartLines = await _accountingContext.JournalEntryLines
                        .Include(l => l.Account)
                        .Where(l => l.JournalEntryId == line.JournalEntryId && l.Id != line.Id)
                        .ToListAsync();

                    var counterpart = counterpartLines
                        .FirstOrDefault(l => !(l.Account?.Code ?? string.Empty).StartsWith("210101"))
                        ?? counterpartLines.FirstOrDefault();

                    if (counterpart == null)
                    {
                        continue;
                    }

                    var counterpartAmount = desiredDebit + desiredCredit;

                    var counterpartNeedsUpdate = candidate.ShPrice < 0
                        ? counterpart.CreditAmount != counterpartAmount || counterpart.DebitAmount != 0
                        : counterpart.DebitAmount != counterpartAmount || counterpart.CreditAmount != 0;

                    if (!counterpartNeedsUpdate)
                    {
                        continue;
                    }

                    if (candidate.ShPrice < 0)
                    {
                        counterpart.CreditAmount = counterpartAmount;
                        counterpart.DebitAmount = 0;
                    }
                    else
                    {
                        counterpart.DebitAmount = counterpartAmount;
                        counterpart.CreditAmount = 0;
                    }

                    updatedLines++;
                }
            }

            if (updatedLines > 0)
            {
                await _accountingContext.SaveChangesAsync();
            }

            return updatedLines;
        }
    }
}
