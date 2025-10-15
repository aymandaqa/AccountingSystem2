using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace AccountingSystem.Models.DynamicScreens
{
    public class DynamicScreenField
    {
        public int Id { get; set; }

        public int ScreenId { get; set; }

        [Required]
        [StringLength(100)]
        public string FieldKey { get; set; } = string.Empty;

        [Required]
        [StringLength(200)]
        public string Label { get; set; } = string.Empty;

        public DynamicScreenFieldType FieldType { get; set; } = DynamicScreenFieldType.Text;

        public DynamicScreenFieldDataSource DataSource { get; set; } = DynamicScreenFieldDataSource.None;

        public DynamicScreenFieldRole Role { get; set; } = DynamicScreenFieldRole.None;

        public bool IsRequired { get; set; }

        public int DisplayOrder { get; set; }

        public int ColumnSpan { get; set; } = 12;

        [StringLength(200)]
        public string? Placeholder { get; set; }

        [StringLength(200)]
        public string? HelpText { get; set; }

        public string? AllowedEntityIds { get; set; }

        public string? MetadataJson { get; set; }

        [ForeignKey(nameof(ScreenId))]
        public virtual DynamicScreenDefinition Screen { get; set; } = null!;
    }
}
