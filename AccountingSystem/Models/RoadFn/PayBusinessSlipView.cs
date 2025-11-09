namespace Roadfn.Models
{
    public partial class PayBusinessSlipView
    {
        public int Id { get; set; }
        public long PaymentHeader { get; set; }
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? ClientName { get; set; }
        public string? CityName { get; set; }
        public DateTime? EntryDate { get; set; }
        public decimal? ShipmentPrice { get; set; }
        public decimal? ShipmentFees { get; set; }
        public decimal? ShipmentExtraFees { get; set; }
        public string? ShipmentTrackingNo { get; set; }
        public string? ArabicCityName { get; set; }
        public string? LoginUserFirstName { get; set; }
        public string? LoginUserLastName { get; set; }
        public decimal? ReturnFees { get; set; }
        public decimal? ShipmentTotal { get; set; }
        public string? AreaName { get; set; }
        public string? ShipmentContains { get; set; }
    }
}
