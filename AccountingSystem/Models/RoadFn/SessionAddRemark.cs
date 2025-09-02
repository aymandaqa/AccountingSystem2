using System;
using System.Collections.Generic;

namespace Roadfn.Models
{
    public partial class SessionAddRemark
    {
        public long Id { get; set; }
        public DateTime? EntryDate { get; set; }
        public DateTime? EntryDateTime { get; set; }
        public long? SessionId { get; set; }
        public long? ShipmentId { get; set; }
        public int? UserId { get; set; }
        public string Remark { get; set; }
        public int? DriverAssignRemarkId { get; set; }
        public int? OldStatus { get; set; }
        public int? NewStatus { get; set; }
        public int? BranchId { get; set; }
    }
}
