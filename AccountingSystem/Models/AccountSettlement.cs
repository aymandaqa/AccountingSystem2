using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class AccountSettlement
    {
        public int Id { get; set; }

        public int AccountId { get; set; }

        [StringLength(450)]
        public string? CreatedById { get; set; }

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public virtual Account Account { get; set; } = null!;
        public virtual User? CreatedBy { get; set; }
        public virtual ICollection<AccountSettlementPair> Pairs { get; set; } = new List<AccountSettlementPair>();
    }

    public class AccountSettlementPair
    {
        public int Id { get; set; }

        public int AccountSettlementId { get; set; }

        public int DebitLineId { get; set; }

        public int CreditLineId { get; set; }

        public virtual AccountSettlement Settlement { get; set; } = null!;
        public virtual JournalEntryLine DebitLine { get; set; } = null!;
        public virtual JournalEntryLine CreditLine { get; set; } = null!;
    }
}
