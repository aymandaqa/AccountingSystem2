using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class DriverMappingAccount
    {
        [Key]

        public string DriverId { get; set; }
        public string AccountId { get; set; }
        public string AccountCode { get; set; }
    }
}

