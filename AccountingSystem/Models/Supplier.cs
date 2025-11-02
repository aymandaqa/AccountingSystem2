using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.ModelBinding.Validation;

namespace AccountingSystem.Models
{
    public class Supplier
    {
        public int Id { get; set; }

        [Required]
        [StringLength(200)]
        public string NameAr { get; set; } = string.Empty;

        [StringLength(200)]
        public string? NameEn { get; set; }

        [StringLength(200)]
        public string? Phone { get; set; }

        [StringLength(200)]
        public string? Email { get; set; }

        public bool IsActive { get; set; } = true;

        public int? AccountId { get; set; }
        public virtual Account? Account { get; set; }

        public SupplierMode Mode { get; set; } = SupplierMode.Cash;

        public SupplierAuthorization AuthorizedOperations { get; set; } = SupplierAuthorization.Payment | SupplierAuthorization.Receipt;

        public virtual ICollection<SupplierBranch> SupplierBranches { get; set; } = new List<SupplierBranch>();

        public string? CreatedById { get; set; }

        [ValidateNever]
        public virtual User? CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
