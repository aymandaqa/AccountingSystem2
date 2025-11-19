using System;
using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models;

namespace AccountingSystem.ViewModels
{
    public class CreateJournalEntryApiRequest
    {

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;
        public decimal Amount { get; set; }

        [StringLength(100)]
        public string? Reference { get; set; }


        public string? BranchId { get; set; }
        public string? BussId { get; set; }

        public string? ApiKey { get; set; }

        public string? UserId { get; set; }

    }


}
