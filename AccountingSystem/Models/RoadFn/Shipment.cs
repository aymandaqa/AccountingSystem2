using System.ComponentModel.DataAnnotations;

namespace Roadfn.Models
{
    public class ShipmentLink
    {
        public int Id { get; set; }
        public string Token { get; set; }           // فريد لكل رابط
        public string EncryptedMerchantId { get; set; }
        public DateTime CreatedAt { get; set; }
        public bool IsUsed { get; set; }
    }
    public partial class Shipment
    {
        public int Id { get; set; }

        [Display(Name = "بزنس؟")]
        public bool? IsUserBusiness { get; set; }
        public int? BusinessUserId { get; set; }

        [Display(Name = "اسم المرسل")]
        public string SenderName { get; set; }

        [Display(Name = "رقم هاتف المرسل")]
        public string SenderTel { get; set; }


        public int? FromCityId { get; set; }

        [Display(Name = "رقم التتبع")]
        public string ShipmentTrackingNo { get; set; }


        public int? ShipmentTypeId { get; set; }
        public int? UserId { get; set; }

        [Display(Name = "الوقت")]
        public DateTime? EntryDate { get; set; }

        [Display(Name = "الوقت")]
        public DateTime? EntryDateTime { get; set; }

        [Display(Name = "الحالة")]
        public int? Status { get; set; }
        public int? DriverId { get; set; }

        [Display(Name = "اسم المستلم")]
        public string ClientName { get; set; }
        public int? ClientCityId { get; set; }
        public int? ClientAreaId { get; set; }

        [Display(Name = "هاتف المستلم")]
        public string ClientPhone { get; set; }

        public string ClientLandLine { get; set; }

        [Display(Name = "عمولة التوصيل")]
        public decimal? ShipmentFees { get; set; }
        public decimal? ShipmentFeesDiscount { get; set; }
        public decimal? PaidAmountFromShipmentFees { get; set; }
        public decimal? DriverExtraComisionValue { get; set; }

        [Display(Name = "مجموع التحصيل")]
        public decimal? ShipmentPrice { get; set; }

        [Display(Name = "عمولة اضافية")]
        public decimal? ShipmentExtraFees { get; set; }

        [Display(Name = "تفاصيل التحصيل")]
        public decimal? ShipmentPriceWithDetail { get; set; }

        [Display(Name = "المجموع الكلي")]
        public decimal? ShipmentTotal { get; set; }

        [Display(Name = "ملاحظات")]
        public string Remarks { get; set; }

        [Display(Name = "اخر حركة")]
        public DateTime? LastUpdate { get; set; }

        [Display(Name = "مرتجع؟")]
        public bool? IsReturn { get; set; }
        public bool? RetPay { get; set; }

        [Display(Name = "عمولة الارجاع")]
        public decimal? ReturnFees { get; set; }

        [Display(Name = "هاتف المستلم 2")]
        public string ClientPhone2 { get; set; }

        [Display(Name = "عنوان المستلم")]
        public string ClientAddress { get; set; }

        [Display(Name = "تنبيه")]
        public string Alert { get; set; }
        public int? BranchId { get; set; }
        public int? rangeVal { get; set; }
        public bool? RetStatus { get; set; }
        public string ShipmentContains { get; set; }
        public string lang { get; set; }
        public long? Shipmets_ConvertToBranch_LOG { get; set; }

        public int? ShipmentQuantity { get; set; }
        public bool? IsForeign { get; set; } = false;
        public string ClientChatUrl { get; set; }
        public string ClientMapAddress { get; set; }
        public bool? RetToCustomer { get; set; }
        public bool? CustomerReceved { get; set; }

        public bool? BranchReceived { get; set; }
        public string BranchSend { get; set; }
        public string BranchRec { get; set; }
        public DateTime? SedToBrnDate { get; set; }



        public decimal? OldShipmentPrice { get; set; }


        public string BrnSendShipmentTransfer { get; set; }
        public string BrnRecShipmentTransfer { get; set; }
        public DateTime? SendShipmentTransferDate { get; set; }
        public bool? BrnRecShipmentTransfersReceved { get; set; }
        public int? ShipmentColorStatus { get; set; }
        public int? MarketerId { get; set; }
        public bool? DriverCanOpenShipment { get; set; }

    }
}
