using System.ComponentModel.DataAnnotations;

namespace EtuRezervasyon.Models
{
    public class LoginRequest
    {
        [Required(ErrorMessage = "Email adresi gereklidir")]
        [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Şifre gereklidir")]
        public string Password { get; set; } = null!;
    }
} 