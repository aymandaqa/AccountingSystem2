using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.Models
{
    public class CusomerMappingAccount
    {

        [Key]
        public string CustomerId { get; set; }
        public string AccountId { get; set; }
        public string AccountCode { get; set; }
    }
}

