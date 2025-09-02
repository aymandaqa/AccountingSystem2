using System;
using System.Collections.Generic;

namespace Roadfn.Models
{
    public partial class ShipmentLog
    {
        public long Id { get; set; }
        public int? ShipmentId { get; set; }
        public DateTime? EntryDate { get; set; }
        public DateTime? EntryDateTine { get; set; }
        public int? UserId { get; set; }
        public int? Status { get; set; }
        public int? DriverId { get; set; }
        public int? BranchId { get; set; }
        public string ClientName { get; set; }
        public int? ClientCityId { get; set; }
        public int? ClientAreaId { get; set; }
        public string ClientPhone { get; set; }
        public string ClientLandLine { get; set; }
        public int? ToBranch { get; set; }
        public bool? IsUserBusiness { get; set; }
        public int? FromCityId { get; set; }
        public int? BusinessUserId { get; set; }
        public string SenderName { get; set; }
        public string SenderTel { get; set; }
    }
}
