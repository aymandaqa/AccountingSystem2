namespace Roadfn.Models
{
    public class CustomerInvoiceRequest
    {
        public int Id { get; set; }
        public string CustomerName { get; set; }
        public DateTime Idate { get; set; }
        public string RecStatus { get; set; }
        public string Note { get; set; }
        public string Branch { get; set; }
        public int CustomerId { get; set; }
    }
}
