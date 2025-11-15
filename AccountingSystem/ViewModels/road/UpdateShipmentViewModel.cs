using System.ComponentModel.DataAnnotations;

namespace Roadfn.ViewModel
{
    public class UpdateShipmentViewModel
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public int BusinessUserID { get; set; }
        public int? Status { get; set; }

        public string SenderName { get; set; }
        public string SenderTel { get; set; }
        public string DateTimeShip { get; set; }
        public string FromCityID { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string ShipmentTrackingNo { get; set; }
        //[Required(ErrorMessage = "الحقل اجباري")]
        public int ShipmentTypeID { get; set; }
        public string UserID { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string ClientName { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public int ClientCityID { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public int ClientAreaID { get; set; }

        [DataType(DataType.PhoneNumber)]
        [StringLength(maximumLength: 10, MinimumLength = 10)]
        [Required(ErrorMessage = "الحقل اجباري")]
        [RegularExpression("^[0-9]*$", ErrorMessage = "الرجاء التأكد من اخال ارقام فقط")]
        public string ClientPhone { get; set; }

        [DataType(DataType.PhoneNumber)]
        [StringLength(maximumLength: 15, MinimumLength = 8)]
        [RegularExpression("^[0-9]*$", ErrorMessage = "الرجاء التأكد من اخال ارقام فقط")]
        public string ClientPhone2 { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string ClientAddress { get; set; }
        public string Alert { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public decimal ShipmentFees { get; set; }
        public decimal ShipmentFeesDiscount { get; set; }


        public decimal PaidAmountFromShipmentFees { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public decimal ShipmentPrice { get; set; }
        [Range(0, int.MaxValue, ErrorMessage = "Please enter a value bigger than {0}")]
        public decimal ShipmentExtraFees { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public decimal ShipmentTotal { get; set; }
        public string Remarks { get; set; }
        public bool IsReturn { get; set; }
        public bool RetPay { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Please enter a value bigger than {1}")]
        public int rangeVal { get; set; }
        public string ShipmentContains { get; set; }

        public int? ShipmentQuantity { get; set; }
        public bool IsForeign { get; set; } = false;
        public string ClientChatUrl { get; set; }
        public string ClientMapAddress { get; set; }

        public bool DriverCanOpenShipment { get; set; } = false;

    }
    public class UpdateBussShipmentViewModel
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public int BusinessUserID { get; set; }

        public string SenderName { get; set; }
        public string SenderTel { get; set; }
        public string DateTimeShip { get; set; }
        public string FromCityID { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string ShipmentTrackingNo { get; set; }
        //[Required(ErrorMessage = "الحقل اجباري")]
        public int ShipmentTypeID { get; set; }
        public string UserID { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string ClientName { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public int ClientCityID { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public int ClientAreaID { get; set; }

        [DataType(DataType.PhoneNumber)]
        [StringLength(maximumLength: 10, MinimumLength = 10)]
        [Required(ErrorMessage = "الحقل اجباري")]
        [RegularExpression("^[0-9]*$", ErrorMessage = "الرجاء التأكد من اخال ارقام فقط")]
        public string ClientPhone { get; set; }

        [DataType(DataType.PhoneNumber)]
        [StringLength(maximumLength: 15, MinimumLength = 8)]
        [RegularExpression("^[0-9]*$", ErrorMessage = "الرجاء التأكد من اخال ارقام فقط")]
        public string ClientPhone2 { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string ClientAddress { get; set; }
        public string Alert { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public decimal ShipmentFees { get; set; }
        public decimal ShipmentFeesDiscount { get; set; }


        public decimal PaidAmountFromShipmentFees { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public decimal ShipmentPrice { get; set; }
        [Range(0, int.MaxValue, ErrorMessage = "Please enter a value bigger than {0}")]
        public decimal ShipmentExtraFees { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]

        [Range(0, int.MaxValue, ErrorMessage = "Please enter a value bigger than {1}")]

        public decimal ShipmentTotal { get; set; }
        public string Remarks { get; set; }
        public bool IsReturn { get; set; }
        public bool RetPay { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Please enter a value bigger than {1}")]
        public int rangeVal { get; set; }
        public string ShipmentContains { get; set; }

        public int? ShipmentQuantity { get; set; }
        public bool IsForeign { get; set; } = false;

        public string ClientChatUrl { get; set; }
        public string ClientMapAddress { get; set; }
    }


    public class UpdateShipmentAdminViewModel
    {
        public int ID { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public int BusinessUserID { get; set; }

        public string SenderName { get; set; }
        public string SenderTel { get; set; }
        public string DateTimeShip { get; set; }
        public string FromCityID { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string ShipmentTrackingNo { get; set; }
        //  [Required(ErrorMessage = "الحقل اجباري")]
        public int ShipmentTypeID { get; set; }
        public string UserID { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string ClientName { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public int ClientCityID { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public int ClientAreaID { get; set; }

        [DataType(DataType.PhoneNumber)]
        [StringLength(maximumLength: 10, MinimumLength = 10)]
        [Required(ErrorMessage = "الحقل اجباري")]
        [RegularExpression("^[0-9]*$", ErrorMessage = "الرجاء التأكد من اخال ارقام فقط")]
        public string ClientPhone { get; set; }

        [DataType(DataType.PhoneNumber)]
        [StringLength(maximumLength: 15, MinimumLength = 8)]
        [RegularExpression("^[0-9]*$", ErrorMessage = "الرجاء التأكد من اخال ارقام فقط")]
        public string ClientPhone2 { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string ClientAddress { get; set; }
        public string Alert { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public decimal ShipmentFees { get; set; }
        public decimal ShipmentFeesDiscount { get; set; }
        public decimal PaidAmountFromShipmentFees { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public decimal ShipmentPrice { get; set; }
        [Range(0, int.MaxValue, ErrorMessage = "Please enter a value bigger than {0}")]
        public decimal ShipmentExtraFees { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public decimal ShipmentTotal { get; set; }
        public string Remarks { get; set; }
        public bool IsReturn { get; set; }
        public bool RetPay { get; set; }
        [Range(1, int.MaxValue, ErrorMessage = "Please enter a value bigger than {1}")]
        public int rangeVal { get; set; }


        public int? Status { get; set; }
        public int? DriverId { get; set; }
        public string ShipmentContains { get; set; }
        public int? ShipmentQuantity { get; set; }
        public bool IsForeign { get; set; } = false;

        public string ClientChatUrl { get; set; }
        public string ClientMapAddress { get; set; }
        public bool DriverCanOpenShipment { get; set; } = false;
    }



    public class UpdateShipmentViewModelbUSS
    {
        public int? ShipmentQuantity { get; set; }

        public int ID { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public int BusinessUserID { get; set; }

        public string SenderName { get; set; }
        public string SenderTel { get; set; }
        public string DateTimeShip { get; set; }
        public string FromCityID { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string ShipmentTrackingNo { get; set; }
        //[Required(ErrorMessage = "الحقل اجباري")]
        public int ShipmentTypeID { get; set; }
        public string UserID { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string ClientName { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public int ClientCityID { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public int ClientAreaID { get; set; }

        [DataType(DataType.PhoneNumber)]
        [StringLength(maximumLength: 10, MinimumLength = 10)]
        [Required(ErrorMessage = "الحقل اجباري")]
        [RegularExpression("^[0-9]*$", ErrorMessage = "الرجاء التأكد من اخال ارقام فقط")]
        public string ClientPhone { get; set; }

        [DataType(DataType.PhoneNumber)]
        [StringLength(maximumLength: 15, MinimumLength = 8)]
        [RegularExpression("^[0-9]*$", ErrorMessage = "الرجاء التأكد من اخال ارقام فقط")]
        public string ClientPhone2 { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string ClientAddress { get; set; }
        public string Alert { get; set; }
        //[Required(ErrorMessage = "الحقل اجباري")]
        public decimal ShipmentFees { get; set; }
        //[Required(ErrorMessage = "الحقل اجباري")]
        public decimal ShipmentPrice { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Please enter a value bigger than {0}")]
        public decimal ShipmentExtraFees { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]

        [Range(0, int.MaxValue, ErrorMessage = "Please enter a value bigger than {1}")]

        public decimal ShipmentTotal { get; set; }
        public string Remarks { get; set; }
        public bool IsReturn { get; set; }
        public bool RetPay { get; set; }
        //  [Range(1, int.MaxValue, ErrorMessage = "Please enter a value bigger than {1}")]
        public int rangeVal { get; set; }
        public string ShipmentContains { get; set; }

        public bool IsForeign { get; set; } = false;
        public bool DriverCanOpenShipment { get; set; } = false;

        public string ClientChatUrl { get; set; }
        public string ClientMapAddress { get; set; }

    }


    public class ApiUpdateShipment
    {
        public int ID { get; set; }

        //  [Required(ErrorMessage = "الحقل اجباري")]
        public int ShipmentTypeID { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public string ClientName { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public int ClientCityID { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public int ClientAreaID { get; set; }

        [DataType(DataType.PhoneNumber)]
        [StringLength(maximumLength: 10, MinimumLength = 10)]
        [Required(ErrorMessage = "الحقل اجباري")]
        [RegularExpression("^[0-9]*$", ErrorMessage = "الرجاء التأكد من اخال ارقام فقط")]
        public string ClientPhone { get; set; }

        [DataType(DataType.PhoneNumber)]
        [StringLength(maximumLength: 15, MinimumLength = 8)]
        [RegularExpression("^[0-9]*$", ErrorMessage = "الرجاء التأكد من اخال ارقام فقط")]
        public string ClientPhone2 { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string ClientAddress { get; set; }

        public string Alert { get; set; }

        // [Required(ErrorMessage = "الحقل اجباري")]
        public decimal? ShipmentFees { get; set; }

        // [Required(ErrorMessage = "الحقل اجباري")]
        public decimal? ShipmentPrice { get; set; }

        [Range(0, int.MaxValue, ErrorMessage = "Please enter a value bigger than {0}")]
        public decimal? ShipmentExtraFees { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public decimal ShipmentTotal { get; set; }

        public string Remarks { get; set; }
        public bool IsReturn { get; set; }
        public bool RetPay { get; set; }

        [Range(1, int.MaxValue, ErrorMessage = "Please enter a value bigger than {1}")]
        public int? rangeVal { get; set; }
        public string ShipmentContains { get; set; }

        public int? ShipmentQuantity { get; set; }
        public bool? IsForeign { get; set; } = false;

        public string ClientChatUrl { get; set; }
        public string ClientMapAddress { get; set; }
        public bool DriverCanOpenShipment { get; set; } = false;
    }
}
