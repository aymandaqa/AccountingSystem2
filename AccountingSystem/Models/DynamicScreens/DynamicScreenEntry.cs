using System;
using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models.Workflows;
using AccountingSystem.Models;

namespace AccountingSystem.Models.DynamicScreens
{
    public class DynamicScreenEntry
    {
        public int Id { get; set; }

        public int ScreenId { get; set; }

        public DynamicScreenEntryStatus Status { get; set; } = DynamicScreenEntryStatus.Draft;

        public decimal Amount { get; set; }

        public bool IsCash { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        public int? ExpenseAccountId { get; set; }

        public int? SupplierId { get; set; }

        public int? BranchId { get; set; }

        public DynamicScreenType ScreenType { get; set; }

        public string DataJson { get; set; } = "{}";

        [StringLength(450)]
        public string CreatedById { get; set; } = string.Empty;

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        [StringLength(450)]
        public string? ApprovedById { get; set; }

        public DateTime? ApprovedAt { get; set; }

        [StringLength(450)]
        public string? RejectedById { get; set; }

        public DateTime? RejectedAt { get; set; }

        public int? WorkflowInstanceId { get; set; }

        public virtual DynamicScreenDefinition Screen { get; set; } = null!;

        public virtual Account? ExpenseAccount { get; set; }

        public virtual Supplier? Supplier { get; set; }

        public virtual Branch? Branch { get; set; }

        public virtual User CreatedBy { get; set; } = null!;

        public virtual User? ApprovedBy { get; set; }

        public virtual User? RejectedBy { get; set; }

        public virtual WorkflowInstance? WorkflowInstance { get; set; }
    }
}
