using System.Collections.Generic;
using System.Linq;
using AccountingSystem.Models;

namespace AccountingSystem.Extensions
{
    public static class SupplierQueryExtensions
    {
        public static IQueryable<Supplier> FilterByAuthorizationAndBranches(
            this IQueryable<Supplier> query,
            SupplierAuthorization requiredAuthorization,
            IReadOnlyCollection<int>? branchIds)
        {
            if (requiredAuthorization != SupplierAuthorization.None)
            {
                query = query.Where(s => (s.AuthorizedOperations & requiredAuthorization) == requiredAuthorization);
            }

            if (branchIds == null || branchIds.Count == 0)
            {
                return query.Where(_ => false);
            }

            return query.Where(s => s.SupplierBranches.Any(sb => branchIds.Contains(sb.BranchId)));
        }
    }
}
