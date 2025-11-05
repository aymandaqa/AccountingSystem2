using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class Asset
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int AssetTypeId { get; set; }

        [Required]
        public int BranchId { get; set; }

        [StringLength(100)]
        public string? AssetNumber { get; set; }

        [StringLength(500)]
        public string? Notes { get; set; }

        public decimal OpeningBalance { get; set; }

        public int? AccountId { get; set; }

        public int? CostCenterId { get; set; }

        public int? SupplierId { get; set; }

        public decimal? PurchaseAmount { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public decimal? OriginalCost { get; set; }

        public decimal? SalvageValue { get; set; }

        public int? DepreciationPeriods { get; set; }

        public DepreciationFrequency? DepreciationFrequency { get; set; }

        public DateTime? PurchaseDate { get; set; }

        public decimal AccumulatedDepreciation { get; set; }

        public decimal BookValue { get; set; }

        public bool IsDisposed { get; set; }

        public DateTime? DisposedAt { get; set; }

        public decimal? DisposalProceeds { get; set; }

        public decimal? DisposalProfitLoss { get; set; }

        public decimal? BookValueAtDisposal { get; set; }

        public virtual Branch Branch { get; set; } = null!;

        public virtual AssetType AssetType { get; set; } = null!;

        public virtual Account? Account { get; set; }

        public virtual CostCenter? CostCenter { get; set; }

        public virtual Supplier? Supplier { get; set; }

        public virtual ICollection<AssetExpense> Expenses { get; set; } = new List<AssetExpense>();

        public virtual ICollection<AssetDepreciation> Depreciations { get; set; } = new List<AssetDepreciation>();
    }
}
