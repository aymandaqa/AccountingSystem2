using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roadfn.Models
{
    public partial class ShipmentsTrackingGeneratedCode
    {
        public long Id { get; set; }
        public int YearId { get; set; }
        public int MonthId { get; set; }
        public int DayId { get; set; }
        public int RecId { get; set; }
        public int? UserBusId { get; set; }
        public int? CodeNum { get; set; }
        public string GeneratedCode { get; set; }
    }

    [Table("BusinessPayShipmentPrice")]
    public class BusinessPayShipmentPrice
    {
        [Key]
        public int Id { get; set; }

        [StringLength(100)]
        public string? ShipmentTrackingNo { get; set; }

        public int? ShipmentId { get; set; }

        public DateTime? EntryDate { get; set; }

        public int? BusinessId { get; set; }

        [StringLength(250)]
        public string? BusinessName { get; set; }

        [StringLength(250)]
        public string? ClientName { get; set; }

        [StringLength(150)]
        public string? CityName { get; set; }

        [StringLength(150)]
        public string? AreaName { get; set; }

        public decimal? ShipmentPrice { get; set; }

        [StringLength(150)]
        public string? Status { get; set; }

        public int? DriverId { get; set; }

        public int? CompanyBranchId { get; set; }
    }
}

