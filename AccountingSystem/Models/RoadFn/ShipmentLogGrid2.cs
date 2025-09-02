namespace Roadfn.Models
{
    public partial class ShipmentLogGrid2
    {
        public long Id { get; set; }
        public DateTime? EntryDate { get; set; }
        public DateTime? EntryDateTime { get; set; }
        public long? SessionId { get; set; }
        public long? ShipmentId { get; set; }
        public string UserName { get; set; }
        public string Remark { get; set; }
        public int? DriverId { get; set; }
        public string DriverName { get; set; }
        public int? FromStatusId { get; set; }
        public string FromStatus { get; set; }
        public int? NewStatusId { get; set; }
        public string NewStatus { get; set; }
        public int? BranchId { get; set; }
        public string BranchName { get; set; }
        public int BusinessUserID { get; set; }
    }
}
