namespace AccountingSystem.Models
{
    public class SupplierBranch
    {
        public int SupplierId { get; set; }

        public virtual Supplier Supplier { get; set; } = null!;

        public int BranchId { get; set; }

        public virtual Branch Branch { get; set; } = null!;
    }
}

