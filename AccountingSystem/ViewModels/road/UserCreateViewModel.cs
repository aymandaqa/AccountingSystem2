using Microsoft.AspNetCore.Mvc;
using System.ComponentModel.DataAnnotations;

namespace Roadfn.ViewModel
{
    public class UserCreateViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public string FirstName { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string LastName { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        [Remote(action: "IsUserNameInUse", controller: "Users")]
        [StringLength(maximumLength: 50, MinimumLength = 5)]
        public string UserName { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string UserPassword { get; set; }


        [StringLength(maximumLength: 10, MinimumLength = 10)]
        [Required(ErrorMessage = "الحقل اجباري")]
        [DataType(DataType.PhoneNumber)]
        [RegularExpression("^[0-9]*$", ErrorMessage = "الرجاء التأكد من اخال ارقام فقط")]
        public string MobileNo1 { get; set; }

        [RegularExpression("^[0-9]*$", ErrorMessage = "الرجاء التأكد من اخال ارقام فقط")]
        [StringLength(maximumLength: 15, MinimumLength = 8)]
        public string MobileNo2 { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public int? CityId { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string Address { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public int? CompanyBranchId { get; set; }

        public int? RefUser { get; set; }
        public int? Driver { get; set; }
        public int? UserType { get; set; }
        public int? BranchFinancialFundId { get; set; }
    }
    public class UserUpdateViewModel
    {
        public int Id { get; set; }
        public string UserName { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string FirstName { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public string LastName { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string UserPassword { get; set; }




        [StringLength(maximumLength: 10, MinimumLength = 10)]
        [Required(ErrorMessage = "الحقل اجباري")]
        [DataType(DataType.PhoneNumber)]
        [RegularExpression("^[0-9]*$", ErrorMessage = "الرجاء التأكد من اخال ارقام فقط")]
        public string MobileNo1 { get; set; }


        [StringLength(maximumLength: 15, MinimumLength = 8)]
        [DataType(DataType.PhoneNumber)]
        [RegularExpression("^[0-9]*$", ErrorMessage = "الرجاء التأكد من اخال ارقام فقط")]
        public string MobileNo2 { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public int? CityId { get; set; }
        [Required(ErrorMessage = "الحقل اجباري")]
        public string Address { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public int? CompanyBranchId { get; set; }

        public int? RefUser { get; set; }
        public int? Driver { get; set; }
        public int? BranchFinancialFundId { get; set; }
        public int? UserType { get; set; }

        [Required(ErrorMessage = "الحقل اجباري")]
        public bool IsActive { get; set; }
    }
}
