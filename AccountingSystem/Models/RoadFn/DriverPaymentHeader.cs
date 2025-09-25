using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Roadfn.Models
{

    [Table("DriverPaymentHeader")]
    public partial class DriverPaymentHeader
    {
        public long Id { get; set; }

        [Display(Name = "السائق ID")]
        public int? DriverId { get; set; }

        [Display(Name = "قيمة الدفعة")]
        public decimal? PaymentValue { get; set; }

        [Display(Name = "تاريخ الدفعة")]
        public DateTime? PaymentDate { get; set; }

        [Display(Name = "المجموع الكلي")]
        public decimal? TotalCod { get; set; }

        [Display(Name = "العمولة")]
        public decimal? DriverComision { get; set; }

        [Display(Name = "مجموع العملات")]
        public decimal? SumOfComison { get; set; }

        [Display(Name = "المستخدم")]
        public int? EntryUserId { get; set; }
        public bool? IsSendTo_IN_OUT_TRANSACTION { get; set; }
    }
}
