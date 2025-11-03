using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class AssetType
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string Name { get; set; } = string.Empty;

        [Required]
        public int AccountId { get; set; }

        public virtual Account Account { get; set; } = null!;

        public bool IsDepreciable { get; set; }

        public int? DepreciationExpenseAccountId { get; set; }

        public int? AccumulatedDepreciationAccountId { get; set; }

        public virtual Account? DepreciationExpenseAccount { get; set; }

        public virtual Account? AccumulatedDepreciationAccount { get; set; }

        public virtual ICollection<Asset> Assets { get; set; } = new List<Asset>();
    }
}
