using System.ComponentModel.DataAnnotations;
using Microsoft.AspNetCore.Mvc.Rendering;

namespace AccountingSystem.ViewModels
{
    public class UserListViewModel
    {
        public string Id { get; set; } = string.Empty;
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public bool IsActive { get; set; }
    }

    public class PermissionSelectionViewModel
    {
        public int PermissionId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsGranted { get; set; }
    }

    public class ManageUserPermissionsViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public List<PermissionSelectionViewModel> Permissions { get; set; } = new List<PermissionSelectionViewModel>();
    }

    public class CreateUserViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        [MinLength(1, ErrorMessage = "يجب اختيار فرع واحد على الأقل")]
        public List<int> BranchIds { get; set; } = new List<int>();
        public int? PaymentAccountId { get; set; }
        public int? PaymentBranchId { get; set; }
        public decimal ExpenseLimit { get; set; }
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> PaymentBranches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> PaymentAccounts { get; set; } = new List<SelectListItem>();
    }

    public class EditUserViewModel
    {
        [Required]
        public string Id { get; set; } = string.Empty;

        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string FirstName { get; set; } = string.Empty;

        [Required]
        [StringLength(100)]
        public string LastName { get; set; } = string.Empty;

        public bool IsActive { get; set; } = true;
        [MinLength(1, ErrorMessage = "يجب اختيار فرع واحد على الأقل")]
        public List<int> BranchIds { get; set; } = new List<int>();
        public int? PaymentAccountId { get; set; }
        public int? PaymentBranchId { get; set; }
        public decimal ExpenseLimit { get; set; }
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> PaymentBranches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> PaymentAccounts { get; set; } = new List<SelectListItem>();
    }
}
