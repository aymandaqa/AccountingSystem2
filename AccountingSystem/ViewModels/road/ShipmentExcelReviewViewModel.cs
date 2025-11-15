using System.Collections.Generic;

namespace Roadfn.ViewModel
{
    public class ShipmentExcelReviewRow
    {
        public int RowNumber { get; set; }
        public string BusinessUserName { get; set; } = string.Empty;
        public CreateShipmentViewModel Shipment { get; set; } = new CreateShipmentViewModel();
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class ShipmentExcelReviewViewModel
    {
        public List<ShipmentExcelReviewRow> Rows { get; set; } = new List<ShipmentExcelReviewRow>();
        public List<string> GeneralErrors { get; set; } = new List<string>();
    }
}
