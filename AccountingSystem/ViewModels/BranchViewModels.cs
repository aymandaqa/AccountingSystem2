using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.ViewModels
{
    public class BranchViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public int UserCount { get; set; }
        public int AccountCount { get; set; }
    }

    public class BranchDetailsViewModel
    {
        public int Id { get; set; }
        public string Code { get; set; } = string.Empty;
        public string NameAr { get; set; } = string.Empty;
        public string NameEn { get; set; } = string.Empty;
        public string? Description { get; set; }
        public string? Address { get; set; }
        public string? Phone { get; set; }
        public string? Email { get; set; }
        public bool IsActive { get; set; }
        public DateTime CreatedAt { get; set; }
        public DateTime? UpdatedAt { get; set; }
        public List<BranchUserViewModel> Users { get; set; } = new List<BranchUserViewModel>();
        public List<BranchAccountViewModel> Accounts { get; set; } = new List<BranchAccountViewModel>();
    }

    public class CreateBranchViewModel
    {
        [Required(ErrorMessage = "كود الفرع مطلوب")]
        [StringLength(20)]
        [Display(Name = "كود الفرع")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم العربي مطلوب")]
        [StringLength(200)]
        [Display(Name = "الاسم العربي")]
        public string NameAr { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "الاسم الإنجليزي")]
        public string NameEn { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [StringLength(500)]
        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [StringLength(20)]
        [Display(Name = "الهاتف")]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        [StringLength(100)]
        [Display(Name = "البريد الإلكتروني")]
        public string? Email { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;
    }

    public class EditBranchViewModel
    {
        public int Id { get; set; }

        [Required(ErrorMessage = "كود الفرع مطلوب")]
        [StringLength(20)]
        [Display(Name = "كود الفرع")]
        public string Code { get; set; } = string.Empty;

        [Required(ErrorMessage = "الاسم العربي مطلوب")]
        [StringLength(200)]
        [Display(Name = "الاسم العربي")]
        public string NameAr { get; set; } = string.Empty;

        [StringLength(200)]
        [Display(Name = "الاسم الإنجليزي")]
        public string NameEn { get; set; } = string.Empty;

        [StringLength(500)]
        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [StringLength(500)]
        [Display(Name = "العنوان")]
        public string? Address { get; set; }

        [StringLength(20)]
        [Display(Name = "الهاتف")]
        public string? Phone { get; set; }

        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        [StringLength(100)]
        [Display(Name = "البريد الإلكتروني")]
        public string? Email { get; set; }

        [Display(Name = "نشط")]
        public bool IsActive { get; set; } = true;
    }

    public class BranchUserViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public bool IsDefault { get; set; }
    }

    public class BranchAccountViewModel
    {
        public int AccountId { get; set; }
        public string AccountCode { get; set; } = string.Empty;
        public string AccountName { get; set; } = string.Empty;
        public string AccountType { get; set; } = string.Empty;
    }

    public class ManageBranchUsersViewModel
    {
        public int BranchId { get; set; }
        public string BranchName { get; set; } = string.Empty;
        public List<BranchUserAssignmentViewModel> Users { get; set; } = new List<BranchUserAssignmentViewModel>();
    }

    public class BranchUserAssignmentViewModel
    {
        public string UserId { get; set; } = string.Empty;
        public string UserName { get; set; } = string.Empty;
        public bool IsAssigned { get; set; }
        public bool IsDefault { get; set; }
    }
}

