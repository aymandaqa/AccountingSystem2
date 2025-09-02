
using System.ComponentModel.DataAnnotations;

namespace Roadfn.Models
{
    public partial class BisnessUserPaymentHeader
    {
        public long Id { get; set; }
        public int? UserId { get; set; }
        public decimal? PaymentValue { get; set; }
        public DateTime? PaymentDate { get; set; }
        public int? LoginUserId { get; set; }
        public int? StatusId { get; set; }
        public int? DriverId { get; set; }
        public bool? IsSendToInOutTransaction { get; set; }
    }

    public partial class BisnessUserReturnHeader
    {
        public long Id { get; set; }
        public int? UserId { get; set; }
        public decimal? PaymentValue { get; set; }
        public DateTime? PaymentDate { get; set; }
        public int? LoginUserId { get; set; }
        public int? StatusId { get; set; }
        public int? DriverId { get; set; }
    }


    public partial class Employees
    {
        public int Id { get; set; }
        public string Name { get; set; }
        public string Mobile { get; set; }
        public string Address { get; set; }
        public decimal? Salary { get; set; }
        public int Branch { get; set; }
        public string BranchName { get; set; }
    }

    public partial class EmployeeLoans
    {
        public long Id { get; set; }
        public string EmployeeName { get; set; }
        public int? EmployeeId { get; set; }
        public DateTime Idate { get; set; }
        public decimal Amount { get; set; }
        public decimal? DueAmount { get; set; }
        public string IUser { get; set; }
        public string Status { get; set; }
        public string Note { get; set; }
    }


    public partial class EmployeeLoansTransactions
    {
        public long Id { get; set; }
        public long LoanId { get; set; }
        public DateTime Idate { get; set; }
        public decimal Amount { get; set; }
        public string IUser { get; set; }
        public string Status { get; set; }
    }


    public partial class User
    {
        public int Id { get; set; }

        [Display(Name = "  مستر ")]
        public string Title { get; set; }

        [Display(Name = " الاسم الأول  ")]
        public string FirstName { get; set; }

        [Display(Name = " اسم العائلة  ")]
        public string LastName { get; set; }

        [Display(Name = "  اسم المستخدم ")]
        public string UserName { get; set; }

        [Display(Name = " كلمة المرور ")]
        public string UserPassword { get; set; }

        [Display(Name = "  نوع المستخدم ")]
        public string UserType { get; set; }
        public int? LanguageId { get; set; }

        [Display(Name = " رقم الهاتف 1  ")]
        public string MobileNo1 { get; set; }

        [Display(Name = "  رقم الهاتف 2 ")]
        public string MobileNo2 { get; set; }

        [Display(Name = " موظف عند بزنس؟  ")]
        public bool? IsBusinessUser { get; set; }
        public bool? IsActive { get; set; }
        public int? CityId { get; set; }

        [Display(Name = "  العنوان ")]
        public string Address { get; set; }

        [Display(Name = " الهاتف ")]
        public string Tel { get; set; }


        public int? CompanyBranchId { get; set; }

        [Display(Name = "")]
        public int? RefUser { get; set; }

        [Display(Name = "سائق؟")]
        public int? Driver { get; set; }

        public DateTime? idate { get; set; } = DateTime.Now;
        public DateTime? NextPasswordChangeDate { get; set; }
        public DateTime? LastPasswordChangeDate { get; set; }
        public int? BranchFinancialFundId { get; set; }
        public DateTime? LastLogin { get; set; }
        public string DeviceToken { get; set; }
        public string AllowStatus { get; set; }
    }


    public partial class MobileRole
    {
        public int Id { get; set; }
        public string RoleName { get; set; }
        public string RoleNote { get; set; }
    }

    public partial class MobileRoleUser
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int RoleId { get; set; }
    }

}

