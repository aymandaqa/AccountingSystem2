using System.ComponentModel.DataAnnotations;

namespace AccountingSystem.ViewModels
{
    public class LoginViewModel
    {
        [Required(ErrorMessage = "البريد الإلكتروني مطلوب")]
        [EmailAddress(ErrorMessage = "البريد الإلكتروني غير صحيح")]
        [Display(Name = "البريد الإلكتروني")]
        public string Email { get; set; } = string.Empty;

        [Required(ErrorMessage = "كلمة المرور مطلوبة")]
        [DataType(DataType.Password)]
        [Display(Name = "كلمة المرور")]
        public string Password { get; set; } = string.Empty;

        public bool LocationConsent { get; set; }

        public double? Latitude { get; set; }

        public double? Longitude { get; set; }

        public double? LocationAccuracy { get; set; }

        public DateTimeOffset? LocationTimestamp { get; set; }

        public string? BrowserName { get; set; }

        public string? BrowserIcon { get; set; }

    }
}

