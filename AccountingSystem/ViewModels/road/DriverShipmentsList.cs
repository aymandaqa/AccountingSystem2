using Roadfn.Models;

namespace Roadfn.ViewModel
{
    public class DriverShipmentsList
    {
        public ShipmentSummaryForMobile Shipment { get; set; }
        public List<ShipmentStatus> ShipmentStatus { get; set; }
    }
}
