using System.Linq;
using AccountingSystem.Models;

namespace AccountingSystem.Extensions
{
    public static class JournalEntryQueryableExtensions
    {
        public static IQueryable<JournalEntryLine> ExcludeCancelled(this IQueryable<JournalEntryLine> source)
        {
            return source.Where(line => line.JournalEntry.Status != JournalEntryStatus.Cancelled);
        }
    }
}
