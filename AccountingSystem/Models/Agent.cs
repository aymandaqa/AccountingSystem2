using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class Agent
    {
        public int Id { get; set; }

        [Required]
        [Display(Name = "الفرع")]
        public int BranchId { get; set; }

        [Required]
        [StringLength(200)]
        [Display(Name = "اسم الوكيل")]
        public string Name { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        public int? AccountId { get; set; }

        public virtual Branch? Branch { get; set; }
        public virtual Account? Account { get; set; }
        public virtual ICollection<User> Users { get; set; } = new List<User>();

        public DateTime CreatedAt { get; set; } = DateTime.Now;
    }
}
