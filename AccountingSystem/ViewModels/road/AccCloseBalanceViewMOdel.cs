namespace Roadfn.ViewModel
{
    public class AccCloseBalanceViewMOdel
    {

        public int Id { get; set; }
        public int BranchId { get; set; }
        public decimal Balance { get; set; }
        public decimal CloseBalance { get; set; }
        public string Ccy { get; set; }
        public string Description { get; set; }
        public decimal Amount { get; set; }
        public string Note { get; set; }
    }
}
