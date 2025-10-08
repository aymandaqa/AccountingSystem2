using System;

namespace AccountingSystem.Models
{
    public class UserPermissionGroup
    {
        public string UserId { get; set; } = string.Empty;
        public int PermissionGroupId { get; set; }
        public DateTime AssignedAt { get; set; } = DateTime.Now;

        public virtual User User { get; set; } = null!;
        public virtual PermissionGroup PermissionGroup { get; set; } = null!;
    }
}
