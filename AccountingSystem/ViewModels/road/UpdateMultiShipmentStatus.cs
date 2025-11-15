namespace Roadfn.ViewModel
{
    public class UpdateMultiShipmentStatus
    {
        public string ShipmentId { get; set; }
        public string StatusId { get; set; }
        public string Note { get; set; }
        public string BranchId { get; set; }
        public string DriverId { get; set; }
    }
    public class UpdateMultiShipmentStatuss
    {
        public string ShipmentsIds { get; set; }
        public string StatusId { get; set; }
        public string Note { get; set; }
        public string BranchId { get; set; }
        public string DriverId { get; set; }
    }

    public class UpdateShipmentStatusForBuss
    {
        public List<int> ShipmentId { get; set; }
        public string StatusId { get; set; }
        public string Note { get; set; }
        public string DriverId { get; set; }
    }



    public class SendToOffice
    {
        public List<int> ShipmentId { get; set; }
        public string DriverId { get; set; }
    }
    public class UpdateShipmentStatusForDriver
    {
        public List<int> ShipmentId { get; set; }
        public string StatusId { get; set; }
        public string Note { get; set; }
    }
}
