using System.ComponentModel.DataAnnotations;

namespace Roadfn.Models
{
    public class BussPaymentsHist
    {
        [Key]
        public long Id { get; set; }

        public long BisnessUserPaymentHeader { get; set; }
        public DateTime Idate { get; set; } = DateTime.Now;
        public int Iuser { get; set; }
        public int DriverId { get; set; }
        public int StatusId { get; set; }
        public string? IuserName { get; set; }
    }
    public class BussRetPaymentsHist
    {
        [Key]
        public long Id { get; set; }

        public long BisnessUserPaymentHeader { get; set; }
        public DateTime Idate { get; set; } = DateTime.Now;
        public int Iuser { get; set; }
        public int DriverId { get; set; }
        public int StatusId { get; set; }
    }
}
