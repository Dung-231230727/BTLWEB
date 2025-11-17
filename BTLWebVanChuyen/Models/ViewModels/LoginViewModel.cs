using System.ComponentModel.DataAnnotations;

namespace BTLWebVanChuyen.ViewModels
{
    public class LoginViewModel
    {
        [Required]
        [EmailAddress]
        public string Email { get; set; } = string.Empty;

        [Required]
        [DataType(DataType.Password)]
        public string Password { get; set; } = string.Empty;

        [Display(Name = "Nhớ đăng nhập?")]
        public bool RememberMe { get; set; }
    }
}
