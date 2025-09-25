using System.ComponentModel.DataAnnotations;

namespace Roadfn.Models
{
    public partial class RptDriverPay
    {
        public int Id { get; set; }

        [Display(Name = "رقم التتبع")]
        public string? ShipmentTrackingNo { get; set; }

        [Display(Name = "رقم البوليصة")]
        public long? ShipmentId { get; set; }

        [Display(Name = "الوقت")]
        public DateTime? EntryDate { get; set; }

        [Display(Name = "اسم السائق")]
        public string? DriverName { get; set; }

        [Display(Name = "اسم المستلم")]
        public string? ClientName { get; set; }

        [Display(Name = "امس المدينة")]
        public string? CityName { get; set; }
        public string? AreaName { get; set; }

        [Display(Name = "المجموع الكلي")]
        public decimal? ShipmentTotal { get; set; }

        [Display(Name = "الحالة السابقة")]
        public int? OldStatus { get; set; }

        [Display(Name = "الحالة الحالية")]
        public int? NewStatus { get; set; }

        [Display(Name = "العمولة")]
        public decimal? CommissionPerItem { get; set; }
        public decimal? ShipmentPrice { get; set; }
        public decimal? PaidAmountFromShipmentFees { get; set; }
        public decimal? DriverExtraComisionValue { get; set; }
        public decimal? ShipmentCod { get; set; }
        public decimal? ShipmentExtraFees { get; set; }

        [Display(Name = "السائق")]
        public int? DriverId { get; set; }
    }
}
