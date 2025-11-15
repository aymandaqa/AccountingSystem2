using Roadfn.Models;

namespace Roadfn.ViewModel
{
    public class ConvertBranchViewModel
    {
        public List<ShipmentSummary> shipments { get; set; }
        public int FromBranch { get; set; }
        public int ToBranch { get; set; }
        public string Note { get; set; }
    }

    public class ConvertBranchViewModel2
    {
        public List<ShipmentSummary> shipments { get; set; }
        public int FromBranch { get; set; }
        public int ToBranch { get; set; }
        public string Note { get; set; }
    }

    public class ConvertBranchRecViewModel
    {
        public List<ShipmentSummary> shipments { get; set; }
        public int ToBranch { get; set; }
    }
    public class ConvertBranchRecViewModel2
    {
        public List<ConvertToBranchView> shipments { get; set; }
        public int ToBranch { get; set; }
    }

    public class ConvertBranchRecInViewModel
    {
        public List<string> shipments { get; set; }
        public string Branch { get; set; }
        public string Type { get; set; }
    }
}
