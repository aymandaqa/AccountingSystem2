namespace Roadfn.Models
{
    public class RPTPaymentHistoryUser
    {
        public Int64 ID { get; set; }
        public int UserID { get; set; }
        public decimal? PaymentValue { get; set; }
        public DateTime? PaymentDate { get; set; }
        public int? LoginUserID { get; set; }
        public int ShipmentCount { get; set; }
        public string? UserName { get; set; }
        public int? BranchNameId { get; set; }
        public string? BranchName { get; set; }
        public decimal? TotalFees { get; set; }
        public decimal? ExtraFees { get; set; }
        public int? DriverId { get; set; }
        public string? Driver { get; set; }
        public string? InputUser { get; set; }
    }
    public class RPTPaymentReturnUsers
    {
        public Int64 ID { get; set; }
        public int UserID { get; set; }
        public decimal? PaymentValue { get; set; }
        public DateTime? PaymentDate { get; set; }
        public int? LoginUserID { get; set; }
        public int ShipmentCount { get; set; }
        public string? UserName { get; set; }
        public int? BranchNameId { get; set; }
        public string? BranchName { get; set; }
        public decimal? TotalFees { get; set; }
        public decimal? ExtraFees { get; set; }
        public int? DriverId { get; set; }
        public string? Driver { get; set; }
        public string? InputUser { get; set; }
    }
}
