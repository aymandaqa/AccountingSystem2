using System.ComponentModel.DataAnnotations;

namespace Roadfn.ViewModel
{
    public class ChangePassword
    {
        [Required(ErrorMessage = "يجب ادخال كلمة المرور الحالية")]
        public string CurrentPassword { get; set; }

        [Required(ErrorMessage = "يجب ادخال كلمة المرور الجديدة")]
        [MinLength(6)]
        public string NewPassword { get; set; }

        [Required(ErrorMessage = "يجب تأكيد كلمة المرور الجديدة")]
        [Compare("NewPassword")]
        [MinLength(6)]
        public string ConfirmPassword { get; set; }
    }
}
