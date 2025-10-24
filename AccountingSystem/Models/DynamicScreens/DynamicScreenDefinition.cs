using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models.Workflows;
using AccountingSystem.Models;

namespace AccountingSystem.Models.DynamicScreens
{
    public class DynamicScreenDefinition
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string DisplayName { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public DynamicScreenType ScreenType { get; set; } = DynamicScreenType.Payment;

        public DynamicScreenPaymentMode PaymentMode { get; set; } = DynamicScreenPaymentMode.CashAndNonCash;

        public bool IsActive { get; set; } = true;

        public int? WorkflowDefinitionId { get; set; }

        public int MenuOrder { get; set; } = 100;

        [StringLength(150)]
        public string PermissionName { get; set; } = string.Empty;

        [StringLength(150)]
        public string ManagePermissionName { get; set; } = string.Empty;

        [StringLength(450)]
        public string? CreatedById { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(450)]
        public string? UpdatedById { get; set; }

        public DateTime? UpdatedAt { get; set; }

        public string? LayoutDefinition { get; set; }

        public virtual WorkflowDefinition? WorkflowDefinition { get; set; }

        public virtual User? CreatedBy { get; set; }

        public virtual User? UpdatedBy { get; set; }

        public virtual ICollection<DynamicScreenField> Fields { get; set; } = new List<DynamicScreenField>();

        public virtual ICollection<DynamicScreenEntry> Entries { get; set; } = new List<DynamicScreenEntry>();
    }
}
