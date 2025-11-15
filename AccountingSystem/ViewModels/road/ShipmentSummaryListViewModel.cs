namespace Roadfn.ViewModel
{
    public class ShipmentSummaryListViewModel
    {
        public int ID { get; set; }
        public string ShipmentTrackingNo { get; set; }
        public string StatusAr { get; set; }
        public string StatusEn { get; set; }
        public int StatusId { get; set; }
        public string ClientName { get; set; }
        public string ClientPhone { get; set; }
        public string ToCityAr { get; set; }
        public string ToCity { get; set; }
        public string AreaName { get; set; }
        public decimal ShipmentTotal { get; set; }
    }
}
