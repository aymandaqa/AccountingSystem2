using System.ComponentModel.DataAnnotations;

namespace Roadfn.Models
{
    public partial class Area
    {
        public long Id { get; set; }

        [Display(Name = "كود المنطقة")]
        public string AreaCode { get; set; }
        [Display(Name = "اسم المنطقة")]
        public string AreaName { get; set; }
        [Display(Name = "رمز المدينة")]

        public int? CityId { get; set; }
        [Display(Name = "الوصف")]

        public string Description { get; set; }

        public DateTime? idate { get; set; } = DateTime.Now;
    }
}
