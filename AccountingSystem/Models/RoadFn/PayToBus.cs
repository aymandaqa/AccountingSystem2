namespace Roadfn.ViewModel
{

    public class PayToBus
    {
        public int Id { get; set; }
        public string? SenderName { get; set; }
        public string? ShipmentTrackingNo { get; set; }
        public int BusinessUserId { get; set; }
        public string? Status { get; set; }
        public string? ShipmentsType { get; set; }
        public string? FromCity { get; set; }
        public string? ClientName { get; set; }
        public string? ClientPhone { get; set; }
        public string? ToCity { get; set; }
        public string? AreaName { get; set; }
        public string? AreaDescription { get; set; }
        public DateTime EntryDateTime { get; set; }
        public decimal ShipmentTotal { get; set; }
        public decimal ShipmentPrice { get; set; }
        public decimal ShipmentExtraFees { get; set; }
        public decimal ShipmentFees { get; set; }
    }
}
