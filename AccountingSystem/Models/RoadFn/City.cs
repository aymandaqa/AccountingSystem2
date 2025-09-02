using System.ComponentModel.DataAnnotations;

namespace Roadfn.Models
{
    public partial class City
    {
        public int Id { get; set; }
        public int? sort { get; set; }
        public string CityCode { get; set; }

        [Display(Name = "اسم المدينة")]
        public string CityName { get; set; }

        [Display(Name = "اسم المدينة باللغة العربية")]
        public string ArabicCityName { get; set; }
        public DateTime? idate { get; set; } = DateTime.Now;
    }


    public class CityCodeBranch
    {
        [Key]
        public string Id { get; set; }
        public string CityCode { get; set; }
    }

    public class CityCodeBranchsum
    {
        [Key]
        public string Id { get; set; }
        public string CityCode { get; set; }
        public int SendCount { get; set; }
        public int RecCount { get; set; }
    }
}
