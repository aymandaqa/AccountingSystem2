using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using AccountingSystem.Models.DynamicScreens;

namespace AccountingSystem.ViewModels.DynamicScreens
{
    public class DynamicScreenEditViewModel
    {
        public int? Id { get; set; }

        [Required]
        [Display(Name = "الاسم الداخلي")]
        public string Name { get; set; } = string.Empty;

        [Required]
        [Display(Name = "اسم الشاشة")]
        public string DisplayName { get; set; } = string.Empty;

        [Display(Name = "الوصف")]
        public string? Description { get; set; }

        [Display(Name = "نوع الشاشة")]
        public DynamicScreenType ScreenType { get; set; } = DynamicScreenType.Payment;

        [Display(Name = "طريقة الدفع")]
        public DynamicScreenPaymentMode PaymentMode { get; set; } = DynamicScreenPaymentMode.CashAndNonCash;

        [Display(Name = "مخطط سير العمل")]
        public int? WorkflowDefinitionId { get; set; }

        [Display(Name = "ترتيب القائمة")]
        public int MenuOrder { get; set; } = 100;

        [Display(Name = "صلاحية الاستخدام")]
        public string PermissionName { get; set; } = string.Empty;

        [Display(Name = "صلاحية الإدارة")]
        public string ManagePermissionName { get; set; } = string.Empty;

        public List<DynamicScreenFieldInputModel> Fields { get; set; } = new();
    }

    public class DynamicScreenFieldInputModel
    {
        public int? Id { get; set; }

        [Required]
        public string FieldKey { get; set; } = string.Empty;

        [Required]
        public string Label { get; set; } = string.Empty;

        public DynamicScreenFieldType FieldType { get; set; } = DynamicScreenFieldType.Text;

        public DynamicScreenFieldDataSource DataSource { get; set; } = DynamicScreenFieldDataSource.None;

        public DynamicScreenFieldRole Role { get; set; } = DynamicScreenFieldRole.None;

        public bool IsRequired { get; set; }

        public int DisplayOrder { get; set; }

        public int ColumnSpan { get; set; } = 12;

        public string? Placeholder { get; set; }

        public string? HelpText { get; set; }

        public string? AllowedEntityIds { get; set; }

        public string? MetadataJson { get; set; }
    }
}
