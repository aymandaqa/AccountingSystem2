using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Roadfn.Models
{
    public partial class BisnessUserPaymentDetail
    {
        public long Id { get; set; }
        public long? HeaderId { get; set; }
        [Display(Name = "رقم البوليصة")]

        public string? ShipmentTrackingNo { get; set; }
        public long? ShipmentId { get; set; }
    }



    public class BisnessUserReturnDetail
    {
        public long Id { get; set; }
        public long? HeaderId { get; set; }
        [Display(Name = "رقم البوليصة")]

        public string? ShipmentTrackingNo { get; set; }
        public long? ShipmentId { get; set; }
    }
}
