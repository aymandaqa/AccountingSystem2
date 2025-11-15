using Roadfn.Models;

namespace Roadfn.ViewModel
{
    public class UpdateStatusViewModel
    {
        public List<ShipmentSummary> Shipments { get; set; }
        public string StatusId { get; set; }
        public string BranchId { get; set; }
        public string DriverId { get; set; }
        public string Note { get; set; }
    }
}
