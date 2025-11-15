using System.ComponentModel.DataAnnotations;

namespace Roadfn.ViewModel
{
    public class ReceivablePayment
    {
        [Required(ErrorMessage = "الحقل اجباري")]
        public int ReceivableId { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public int BranchId { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please enter a value bigger than {1}")]

        [Required(ErrorMessage = "الحقل اجباري")]
        public decimal Amount { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public int TransactionType { get; set; }
        public string Note { get; set; }
    }
}
