using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.ViewModels.DynamicScreens
{
    public class DynamicScreenEntryInputModel
    {
        [Required]
        public int ScreenId { get; set; }

        public bool IsCash { get; set; }

        public int? BranchId { get; set; }

        public List<DynamicScreenEntryFieldValue> Fields { get; set; } = new();
    }

    public class DynamicScreenEntryFieldValue
    {
        [Required]
        public int FieldId { get; set; }

        public string? Value { get; set; }
    }
}
