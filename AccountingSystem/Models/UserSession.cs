using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models
{
    public class UserSession
    {
        [Key]
        public Guid Id { get; set; } = Guid.NewGuid();

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string SessionId { get; set; } = string.Empty;

        [StringLength(100)]
        public string? DeviceType { get; set; }

        [StringLength(200)]
        public string? DeviceName { get; set; }

        [StringLength(200)]
        public string? OperatingSystem { get; set; }

        [StringLength(100)]
        public string? IpAddress { get; set; }

        [StringLength(1000)]
        public string? UserAgent { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public DateTime? LastActivityAt { get; set; } = DateTime.UtcNow;

        public DateTime? EndedAt { get; set; }

        [StringLength(200)]
        public string? EndedReason { get; set; }

        public bool IsActive { get; set; } = true;

        [ForeignKey(nameof(UserId))]
        public virtual User User { get; set; } = null!;
    }
}

