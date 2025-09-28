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

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public DateTime? UpdatedAt { get; set; }

        public virtual Branch Branch { get; set; } = null!;

        public virtual AssetType AssetType { get; set; } = null!;

        public virtual Account? Account { get; set; }

        public virtual ICollection<AssetExpense> Expenses { get; set; } = new List<AssetExpense>();
    }
}
