using System.ComponentModel.DataAnnotations;

namespace Roadfn.Models
{
    public partial class ShipmentFee
    {
        public int Id { get; set; }
        public int? FromCityId { get; set; }
        public int? ToCityId { get; set; }
        public int? ToAreaId { get; set; }
        public bool? IsBusiness { get; set; }
        public int? UserId { get; set; }
        public decimal? Fees { get; set; }
        public int? UserBusinessId { get; set; }
        public decimal? ReturnFees { get; set; }
    }
    public class AreaGeneralFee
    {
        [Key]
        public int Id { get; set; }

        public int? CityId { get; set; }

        [Required]
        public int AreaId { get; set; }

        public decimal? Fees { get; set; }

        public decimal? ReturnFees { get; set; }
    }
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
        public bool? NeedToSellect_Shipment_And_EX { get; set; }
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
