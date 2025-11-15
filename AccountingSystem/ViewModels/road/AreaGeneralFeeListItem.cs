namespace Roadfn.ViewModel
{
    public class AreaGeneralFeeListItem
    {
        public int Id { get; set; }
        public int CityId { get; set; }
        public string CityName { get; set; } = string.Empty;
        public int AreaId { get; set; }
        public string AreaName { get; set; } = string.Empty;
        public decimal Fees { get; set; }
        public decimal ReturnFees { get; set; }
    }
}
