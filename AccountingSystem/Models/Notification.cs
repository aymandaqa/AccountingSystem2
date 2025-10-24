using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models.Workflows;

namespace AccountingSystem.Models
{
    public class Notification
    {
        public int Id { get; set; }

        [Required]
        [StringLength(450)]
        public string UserId { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Title { get; set; } = string.Empty;

        [StringLength(1000)]
        public string? Message { get; set; }

        [StringLength(500)]
        public string? Link { get; set; }

        public bool IsRead { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        [StringLength(100)]
        public string? Icon { get; set; }

        public int? WorkflowActionId { get; set; }

        public virtual User User { get; set; } = null!;

        public virtual WorkflowAction? WorkflowAction { get; set; }
    }
}
