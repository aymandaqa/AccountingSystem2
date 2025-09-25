using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Roadfn.Models
{
    public partial class CompanyBranch
    {
        public int Id { get; set; }

        [Display(Name = "اسم الفرع")]
        public string? BranchName { get; set; }
        public int? CityId { get; set; }
    }
}
