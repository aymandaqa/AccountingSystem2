using System.Collections.Generic;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class SuppliersIndexViewModel
    {
        public IEnumerable<SelectListItem> BranchOptions { get; set; } = new List<SelectListItem>();

        public IEnumerable<SelectListItem> SupplierTypeOptions { get; set; } = new List<SelectListItem>();

        public decimal PositiveBalanceTotal { get; set; }

        public decimal NegativeBalanceTotal { get; set; }
    }
}
