namespace Roadfn.Models
{
    public partial class ShipmentStatus
    {
        public int Id { get; set; }
        public string? Description { get; set; }
        public string? ArabicDescription { get; set; }
        public string? Icon { get; set; }
        public bool? IsVisibleInTransaction { get; set; }
        public string? ButtunStatusListValid { get; set; }
        public bool? IsNeedDriverAssign { get; set; }
        public bool? IsNeedLocationOnly { get; set; }
        public bool? IsValidForUser { get; set; }
        public bool? IsValidForDriver { get; set; }
        public bool? NeedToSellectShipmentAndEx { get; set; }
        public bool? IsReturn { get; set; }
        public bool? ExStart { get; set; }
        public bool? NeedAttention { get; set; }
        public bool? EnableEditForAdmin { get; set; }
        public bool? EnableEditForBuss { get; set; }
        public bool? NeedConfirmation { get; set; }
        public bool? RequiredReceivedFromBranch { get; set; }
        public bool? RequiredTransferToBranch { get; set; }
        public string? LinkToStatusRef { get; set; }
        public string? FullDescription { get; set; }
        public string? IconColore { get; set; }
    }
}
