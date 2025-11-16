using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.ViewModels
{
    public class SupplierTypeFormViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "نوع المورد")]
        public string Name { get; set; } = string.Empty;

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;
    }
}
