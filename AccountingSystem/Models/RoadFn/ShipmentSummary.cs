namespace Roadfn.Models
{
    public class ShipmentSummary
    {
        public int ID { get; set; }
        public int BusinessUserID { get; set; }
        public string SenderName { get; set; }
        public string ShipmentTrackingNo { get; set; }
        public string BranchName { get; set; }
        public int? BranchNameId { get; set; }
        public string StatusAr { get; set; }
        public string StatusEn { get; set; }
        public int StatusId { get; set; }
        public string ShipmentsType { get; set; }
        public string FromCityAr { get; set; }
        public string FromCity { get; set; }
        public int? FromCityId { get; set; }
        public string ClientName { get; set; }
        public string ClientPhone { get; set; }
        public string ClientPhone2 { get; set; }
        public string ToCityAr { get; set; }
        public string ToCity { get; set; }
        public int? ToCityId { get; set; }
        public string AreaName { get; set; }
        public long? AreaId { get; set; }
        public string AreaDescription { get; set; }
        public decimal ShipmentTotal { get; set; }
        public decimal? ShipmentPrice { get; set; }
        public decimal? OldShipmentPrice { get; set; }
        public decimal? ShipmentExtraFees { get; set; }
        public decimal? ShipmentFees { get; set; }
        public string Alert { get; set; }
        public int? DriverID { get; set; }
        public int? DriverLoginUserID { get; set; }
        public string DriverName { get; set; }
        public DateTime EntryDateTime { get; set; }
        public DateTime LastUpdate { get; set; }
        public DateTime SedToBrnDate { get; set; }
        public int NeedAttentionHour { get; set; }
        public string Remarks { get; set; }
        public string ClientAddress { get; set; }
        public string DriverPhone { get; set; }
        public string ShipmentContains { get; set; }
        public string Note { get; set; }
        public string lang { get; set; }
        public int? ShipmentQuantity { get; set; }
        public bool? IsForeign { get; set; } = false;
        public string CityCode { get; set; }



        public bool? BranchReceived { get; set; }
        public string BranchSend { get; set; }
        public string BranchRec { get; set; }

        public string BrnSendShipmentTransfer { get; set; }
        public string BrnRecShipmentTransfer { get; set; }
        public DateTime SendShipmentTransferDate { get; set; }
        public bool? BrnRecShipmentTransfersReceved { get; set; }

        public int? StatusColorId { get; set; }
        public string StatusColorName { get; set; }
        public string StatusColorNameColor { get; set; }
        public bool? DriverCanOpenShipment { get; set; }
        public int? UserID { get; set; }
        public string IUser { get; set; }
    }
    public class ShipmentSummaryForMobile
    {
        public int ID { get; set; }
        public int BusinessUserID { get; set; }
        public string SenderName { get; set; }
        public string ShipmentTrackingNo { get; set; }
        public string IconColore { get; set; }
        public string StatusAr { get; set; }
        public string StatusEn { get; set; }
        public int StatusId { get; set; }
        public string ShipmentsType { get; set; }
        public string FromCityAr { get; set; }
        public string FromCity { get; set; }
        public int? FromCityId { get; set; }
        public string ClientName { get; set; }
        public string ClientPhone { get; set; }
        public string ClientPhone2 { get; set; }
        public string ToCityAr { get; set; }
        public string ToCity { get; set; }
        public int? ToCityId { get; set; }
        public string AreaName { get; set; }
        public long? AreaId { get; set; }
        public string AreaDescription { get; set; }
        public decimal ShipmentTotal { get; set; }
        public decimal? ShipmentPrice { get; set; }
        public decimal? ShipmentExtraFees { get; set; }
        public string Alert { get; set; }
        public int? DriverID { get; set; }
        public int? DriverLoginUserID { get; set; }
        public string DriverName { get; set; }
        public DateTime EntryDateTime { get; set; }
        public DateTime LastUpdate { get; set; }
        public string Remarks { get; set; }
        public string ClientAddress { get; set; }
        public string DriverPhone { get; set; }
        public string ShipmentContains { get; set; }
        public string Note { get; set; }
        public string lang { get; set; }
        public int? ShipmentQuantity { get; set; }
        public bool? IsForeign { get; set; } = false;
        public bool? IsReturn { get; set; } = false;
        public bool? RetStatus { get; set; } = false;
        public string ClientChatUrl { get; set; }
        public string ClientMapAddress { get; set; }
        public int ShipmentChatCount { get; set; }
        public bool? DriverCanOpenShipment { get; set; }
        public string StatusColorName { get; set; }
        public string StatusColorNameColor { get; set; }
        public int? UserID { get; set; }
        public string IUser { get; set; }



    }
}
