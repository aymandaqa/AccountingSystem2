using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Roadfn.ViewModel
{
    public class ShipmentFeeNew
    {
        [Required]
        public string[] FromCity { get; set; }

        [Required]
        public string[] ToCity { get; set; }

        [Required]
        public string UserId { get; set; }

        [Required]
        public decimal Fee { get; set; }

        [Required]
        public decimal Ree { get; set; }
    }
}
