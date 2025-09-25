using System.ComponentModel.DataAnnotations;

namespace Roadfn.Models
{
    public class InvoiceStatus
    {
        [Key]
        public int Id { get; set; }
        public int TransferShipmentStatusTo { get; set; }
        public string? StatusName { get; set; }
        public string? StatusDesc { get; set; }
    }
    public class ShipmentColorStatus
    {
        [Key]
        public int Id { get; set; }
        public string? StatusName { get; set; }
        public string? StatusColor { get; set; }
    }
}
