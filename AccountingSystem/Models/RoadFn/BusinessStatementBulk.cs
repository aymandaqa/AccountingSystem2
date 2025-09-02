using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roadfn.Models
{
    public class BusinessStatementBulk
    {
        [Key]
        public int SenderId { get; set; }
        public string SenderName { get; set; }
        public int StatusId { get; set; }
        public string StatusDesc { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentFees { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentPrice { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentTotal { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentExtraFees { get; set; }
        public int ShipmentsNumber { get; set; }
        public int CompanyBranchID { get; set; }

    }
    public class MBusinessStatementBulk
    {
        [Key]
        public int SenderId { get; set; }
        public string SenderName { get; set; }
        public int StatusId { get; set; }
        public string StatusDesc { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentFees { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentPrice { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentTotal { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentExtraFees { get; set; }
        public int ShipmentsNumber { get; set; }
        public int CompanyBranchID { get; set; }
        public int? UserID { get; set; }
        public string IUser { get; set; }
    }
    public class BusinessRetStatementBulk
    {
        [Key]
        public Guid Id { get; set; }
        public int SenderId { get; set; }
        public string SenderName { get; set; }
        public int StatusId { get; set; }
        public string StatusDesc { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentFees { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentPrice { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentTotal { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentExtraFees { get; set; }
        public int ShipmentsNumber { get; set; }
        public int CompanyBranchID { get; set; }

    }
    public class BusinessReturnedBulk
    {
        [Key]
        public int SenderId { get; set; }
        public string SenderName { get; set; }
        public int StatusId { get; set; }
        public string StatusDesc { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentFees { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentPrice { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentTotal { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentExtraFees { get; set; }
        public int ShipmentsNumber { get; set; }
        public int CompanyBranchID { get; set; }

    }
}
