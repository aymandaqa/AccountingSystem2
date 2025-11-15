using System;
using System.Collections.Generic;

namespace Roadfn.Models
{
    public partial class ShipmentsType
    {
        public int Id { get; set; }
        public string? Description { get; set; }
        public string? Alert { get; set; }
    }

    public class GetShipmentByClinteMobileView
    {
        public Guid Id { get; set; }
        public int TheCount { get; set; }
        public int NewStatus { get; set; }
        public int OldStatus { get; set; }
        public string ClientPhone { get; set; }
    }
}
