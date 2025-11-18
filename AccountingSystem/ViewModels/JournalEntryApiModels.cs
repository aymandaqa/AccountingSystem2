using System;
using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models;

namespace AccountingSystem.ViewModels
{
    public class CreateJournalEntryApiRequest
    {
        [Required]
        public DateTime Date { get; set; } = DateTime.Today;

        [Required]
        [StringLength(500)]
        public string Description { get; set; } = string.Empty;

        [StringLength(100)]
        public string? Reference { get; set; }

        [StringLength(20)]
        public string? Number { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "يجب اختيار الفرع.")]
        public int BranchId { get; set; }

        public JournalEntryStatus Status { get; set; } = JournalEntryStatus.Draft;

        public List<CreateJournalEntryLineApiRequest> Lines { get; set; } = new();
    }

    public class CreateJournalEntryLineApiRequest
    {
        [Range(1, int.MaxValue, ErrorMessage = "يجب اختيار الحساب.")]
        public int AccountId { get; set; }

        [StringLength(500)]
        public string? Description { get; set; }

        [StringLength(100)]
        public string? Reference { get; set; }

        [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "يجب أن يكون المبلغ المدين أكبر من أو يساوي صفر.")]
        public decimal DebitAmount { get; set; }

        [Range(typeof(decimal), "0", "79228162514264337593543950335", ErrorMessage = "يجب أن يكون المبلغ الدائن أكبر من أو يساوي صفر.")]
        public decimal CreditAmount { get; set; }

        public int? CostCenterId { get; set; }
    }

    public class JournalEntryApiResponse
    {
        public int Id { get; set; }
        public string Number { get; set; } = string.Empty;
        public DateTime Date { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public JournalEntryStatus Status { get; set; }
        public int BranchId { get; set; }
        public int? CashAccountId { get; set; }
        public decimal TotalDebit { get; set; }
        public decimal TotalCredit { get; set; }
        public List<JournalEntryLineApiResponse> Lines { get; set; } = new();
    }

    public class JournalEntryLineApiResponse
    {
        public int AccountId { get; set; }
        public string Description { get; set; } = string.Empty;
        public string? Reference { get; set; }
        public decimal DebitAmount { get; set; }
        public decimal CreditAmount { get; set; }
        public int? CostCenterId { get; set; }
    }
}
