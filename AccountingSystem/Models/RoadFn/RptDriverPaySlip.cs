namespace Roadfn.Models
{
    public class RptDriverPaySlip
    {
        public long ID { get; set; }
        public string ShipmentTrackingNo { get; set; }
        public string SenderName { get; set; }
        public string CityName { get; set; }
        public string AreaName { get; set; }
        public DateTime EntryDate { get; set; }
        public decimal? ShipmentPrice { get; set; }
        public decimal? ShipmentFees { get; set; }
        public decimal? DriverExtraComisionValue { get; set; }
        public decimal? ShipmentExtraFees { get; set; }
        public decimal? ComisionValue { get; set; }
        public long HeaderID { get; set; }
    }
}
