using System.ComponentModel.DataAnnotations;

namespace Roadfn.ViewModel
{
    public partial class RptpaymentHistoryDriver
    {
        public long Id { get; set; }

        [Display(Name = "  تاريخ الدفعة ")]
        public DateTime? PaymentDate { get; set; }

        [Display(Name = " قيمة الدفعة  ")]
        public decimal? PaymentValue { get; set; }
        public int? DriverId { get; set; }

        [Display(Name = "مجموع التحصيلات ")]
        public decimal? TotalCod { get; set; }

        [Display(Name = " عمولة السائق  ")]
        public decimal? DriverComision { get; set; }

        [Display(Name = " مجموه العمولة  ")]
        public decimal? SumOfComison { get; set; }
        public decimal? Total { get; set; }
        public int? EntryUserId { get; set; }
        public string DriverName { get; set; }
        public string EntryUser { get; set; }
        public int? BranchNameId { get; set; }
        public int? shipmentCount { get; set; }
    }
}
