using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Roadfn.Models
{
    public partial class DriverPaymentDetail
    {
        public long Id { get; set; }
        public long? HeaderId { get; set; }

        [Display(Name = "رقم التتبع")]
        public string ShipmentTrackingNo { get; set; }
        public long? ShipmentId { get; set; }

        [Display(Name = "قيمة العمولة")]
        public decimal? ComisionValue { get; set; }
        public decimal? CompanyRevenueValue { get; set; }
        public decimal? DriverExtraComisionValue { get; set; }
    }
}
