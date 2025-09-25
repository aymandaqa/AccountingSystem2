using System.ComponentModel.DataAnnotations;

namespace Roadfn.Models
{
    public class DriverPay
    {
        [Key]
        public int DriverID { get; set; }
        public string? DriverName { get; set; }
        public int BranchID { get; set; }
    }
}




