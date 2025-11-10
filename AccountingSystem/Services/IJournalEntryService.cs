using AccountingSystem.Models;
using System.Collections.Generic;
using System.Threading.Tasks;

namespace AccountingSystem.Services
{
    public interface IJournalEntryService
    {
        Task<JournalEntry> CreateJournalEntryAsync(
            System.DateTime date,
            string description,
            int branchId,
            string createdById,
            IEnumerable<JournalEntryLine> lines,
            JournalEntryStatus status,
            string? reference = null,
            string? number = null,
            string? approvedById = null);

        Task<string> GenerateJournalEntryNumber();

    }
}

