using System.Collections.Generic;

namespace Roadfn.ViewModel
{
    public class BusinessShipmentExcelReviewRow
    {
        public int RowNumber { get; set; }
        public BussCreateShipmentExcelViewModel Shipment { get; set; } = new BussCreateShipmentExcelViewModel();
        public string AreaName { get; set; } = string.Empty;
        public string CityName { get; set; } = string.Empty;
        public List<string> Errors { get; set; } = new List<string>();
    }

    public class BusinessShipmentExcelReviewViewModel
    {
        public List<BusinessShipmentExcelReviewRow> Rows { get; set; } = new List<BusinessShipmentExcelReviewRow>();
        public List<string> GeneralErrors { get; set; } = new List<string>();
        public List<BusinessShipmentCityOptionViewModel> CityOptions { get; set; } = new List<BusinessShipmentCityOptionViewModel>();
        public List<BusinessShipmentAreaOptionViewModel> AreaOptions { get; set; } = new List<BusinessShipmentAreaOptionViewModel>();
    }

    public class BusinessShipmentCityOptionViewModel
    {
        public int Id { get; set; }
        public string Name { get; set; } = string.Empty;
    }

    public class BusinessShipmentAreaOptionViewModel
    {
        public long Id { get; set; }
        public string Name { get; set; } = string.Empty;
        public int? CityId { get; set; }
        public string CityName { get; set; } = string.Empty;
    }
}
