using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.ViewModels
{
    public class PermissionGroupListItemViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PermissionsCount { get; set; }
        public int UsersCount { get; set; }
        public DateTime CreatedAt { get; set; }
    }

    public class EditPermissionGroupViewModel
    {
        public int? Id { get; set; }

        [Required]
        [StringLength(150)]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        public string? Description { get; set; }

        public List<PermissionSelectionViewModel> Permissions { get; set; } = new List<PermissionSelectionViewModel>();
    }
}
