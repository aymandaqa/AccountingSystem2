using System;
using System.Collections.Generic;
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
        public DateTime? LastLoginAt { get; set; }
        public string AgentName { get; set; } = string.Empty;
    }

    public class UsersIndexViewModel
    {
        public List<UserListViewModel> Users { get; set; } = new List<UserListViewModel>();
        public string SearchTerm { get; set; } = string.Empty;
        public int CurrentPage { get; set; }
        public int TotalPages { get; set; }
        public int PageSize { get; set; }
        public int TotalCount { get; set; }
    }

    public class PermissionSelectionViewModel
    {
        public int PermissionId { get; set; }
        public string DisplayName { get; set; } = string.Empty;
        public string Category { get; set; } = string.Empty;
        public bool IsGranted { get; set; }
        public bool IsInherited { get; set; }
        public bool HasDirectGrant { get; set; }
    }

    public class PermissionGroupSelectionViewModel
    {
        public int PermissionGroupId { get; set; }
        public string Name { get; set; } = string.Empty;
        public string? Description { get; set; }
        public int PermissionsCount { get; set; }
        public bool IsAssigned { get; set; }
        public List<int> PermissionIds { get; set; } = new List<int>();
    }

    public class ManageUserPermissionsViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public List<PermissionSelectionViewModel> Permissions { get; set; } = new List<PermissionSelectionViewModel>();
        public List<PermissionGroupSelectionViewModel> Groups { get; set; } = new List<PermissionGroupSelectionViewModel>();
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
        public int? PaymentBranchId { get; set; }
        public decimal ExpenseLimit { get; set; }
        public List<int> DriverAccountBranchIds { get; set; } = new List<int>();
        public List<int> BusinessAccountBranchIds { get; set; } = new List<int>();
        [Display(Name = "الوكيل")]
        public int? AgentId { get; set; }
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> PaymentBranches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> DriverBranches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> BusinessBranches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Agents { get; set; } = new List<SelectListItem>();
        public List<UserCurrencyAccountViewModel> CurrencyAccounts { get; set; } = new List<UserCurrencyAccountViewModel>();
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
        public int? PaymentBranchId { get; set; }
        public decimal ExpenseLimit { get; set; }
        public List<int> DriverAccountBranchIds { get; set; } = new List<int>();
        public List<int> BusinessAccountBranchIds { get; set; } = new List<int>();
        [Display(Name = "الوكيل")]
        public int? AgentId { get; set; }
        public List<SelectListItem> Branches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> PaymentBranches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> DriverBranches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> BusinessBranches { get; set; } = new List<SelectListItem>();
        public List<SelectListItem> Agents { get; set; } = new List<SelectListItem>();
        public List<UserCurrencyAccountViewModel> CurrencyAccounts { get; set; } = new List<UserCurrencyAccountViewModel>();
    }

    public class ResetUserPasswordViewModel
    {
        [Required]
        public string Id { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "كلمتا المرور غير متطابقتين")] 
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class ProfileViewModel
    {
        public string Email { get; set; } = string.Empty;
        public string FullName { get; set; } = string.Empty;
        public DateTime? LastLoginAt { get; set; }

        [Required]
        [DataType(DataType.Password)]
        public string CurrentPassword { get; set; } = string.Empty;

        [Required]
        [StringLength(100, MinimumLength = 6)]
        [DataType(DataType.Password)]
        public string NewPassword { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        [Compare("NewPassword", ErrorMessage = "كلمتا المرور غير متطابقتين")] 
        public string ConfirmPassword { get; set; } = string.Empty;
    }

    public class UserCurrencyAccountViewModel
    {
        public int CurrencyId { get; set; }
        public string CurrencyName { get; set; } = string.Empty;
        public int? AccountId { get; set; }
        public List<SelectListItem> Accounts { get; set; } = new List<SelectListItem>();
    }
}
