namespace AccountingSystem.Models
{
    public class PermissionGroupPermission
    {
        public int PermissionGroupId { get; set; }
        public int PermissionId { get; set; }

        public virtual PermissionGroup PermissionGroup { get; set; } = null!;
        public virtual Permission Permission { get; set; } = null!;
    }
}
