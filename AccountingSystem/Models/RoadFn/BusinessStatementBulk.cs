using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roadfn.Models
{
    public class BusinessStatementBulk
    {
        [Key]
        public int SenderId { get; set; }
        public string? SenderName { get; set; }
        public int StatusId { get; set; }
        public string? StatusDesc { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentFees { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentPrice { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentTotal { get; set; }
        [Column(TypeName = "decimal(18, 2)")]
        public decimal ShipmentExtraFees { get; set; }

        [Column(TypeName = "decimal(18, 2)")]
        public decimal PaidAmountFromShipmentFees { get; set; }
        public int ShipmentsNumber { get; set; }
        public int CompanyBranchID { get; set; }

    }
    public class MBusinessStatementBulk
    {
        [Key]
        public int SenderId { get; set; }
        public string? SenderName { get; set; }
        public int StatusId { get; set; }
        public string? StatusDesc { get; set; }
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
        public string? IUser { get; set; }
    }
    public class BusinessClient
    {
        [Key]
        public int Id { get; set; }
        public int? BisnessUserId { get; set; }
        public int? City { get; set; }
        public int? Area { get; set; }
        public string? Mobile1 { get; set; }
        public string? Mobile2 { get; set; }
        public string? Address { get; set; }
        public string? Name { get; set; }
    }
    public class BusinessRetStatementBulk
    {
        [Key]
        public Guid Id { get; set; }
        public int SenderId { get; set; }
        public string? SenderName { get; set; }
        public int StatusId { get; set; }
        public string? StatusDesc { get; set; }
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
        public string? SenderName { get; set; }
        public int StatusId { get; set; }
        public string? StatusDesc { get; set; }
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
