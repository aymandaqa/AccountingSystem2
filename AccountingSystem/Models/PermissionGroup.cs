using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class PermissionGroup
    {
        public int Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual ICollection<PermissionGroupPermission> PermissionGroupPermissions { get; set; } = new List<PermissionGroupPermission>();
        public virtual ICollection<UserPermissionGroup> UserPermissionGroups { get; set; } = new List<UserPermissionGroup>();
    }
}
