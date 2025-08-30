namespace AccountingSystem.Models
{
    public class UserBranch
    {
        public string UserId { get; set; } = string.Empty;
        public int BranchId { get; set; }
        public bool IsDefault { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        public virtual User User { get; set; } = null!;
        public virtual Branch Branch { get; set; } = null!;
    }
}

