namespace AccountingSystem.ViewModels
{
    public class SupplierTypeListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public bool IsActive { get; set; }
        public int SuppliersCount { get; set; }
    }
}
