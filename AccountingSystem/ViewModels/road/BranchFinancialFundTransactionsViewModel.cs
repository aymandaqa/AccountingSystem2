using System.ComponentModel.DataAnnotations;

namespace Roadfn.ViewModel
{
    public class BranchFinancialFundTransactionsViewModel
    {
        [Required(ErrorMessage = "الحقل اجباري")]
        public int BranchFinancialFundId { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        [Range(1, int.MaxValue, ErrorMessage = "Please enter a value bigger than {1}")]
        public decimal Amount { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Please enter a value bigger than {1}")]

        public decimal? DiscountAmount { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string Note { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string TransactionReference { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public int TransactionType { get; set; }

        public int? RefEmpId { get; set; }
        public int? ReceverAccountId { get; set; }
        public int? RefBussId { get; set; }
        public int? LoanId { get; set; }


    }
}
