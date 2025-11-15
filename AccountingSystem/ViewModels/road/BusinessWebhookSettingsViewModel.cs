using System.ComponentModel.DataAnnotations;

namespace Roadfn.ViewModel
{
    public class BusinessWebhookSettingsViewModel
    {
        [Display(Name = "رابط الـ API")]
        [Required(ErrorMessage = "الرجاء إدخال رابط الـ API")]
        [Url(ErrorMessage = "الرجاء إدخال رابط صحيح")]
        public string EndpointUrl { get; set; }

        [Display(Name = "مفتاح API (اختياري)")]
        public string ApiKey { get; set; }

        [Display(Name = "اسم المستخدم (اختياري)")]
        public string UserName { get; set; }

        [Display(Name = "كلمة المرور / التوكن (اختياري)")]
        [DataType(DataType.Password)]
        public string Password { get; set; }

        [Display(Name = "تفعيل الإشعارات")]
        public bool IsEnabled { get; set; }

        public DateTime? LastTriggeredAt { get; set; }
    }
}
