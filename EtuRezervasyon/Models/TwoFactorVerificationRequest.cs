using System.ComponentModel.DataAnnotations;

namespace EtuRezervasyon.Models
{
    public class TwoFactorVerificationRequest
    {
        [Required(ErrorMessage = "Email adresi gereklidir")]
        [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz")]
        public string Email { get; set; } = null!;

        [Required(ErrorMessage = "Doğrulama kodu gereklidir")]
        [StringLength(6, MinimumLength = 4, ErrorMessage = "Doğrulama kodu 4-6 karakter arasında olmalıdır")]
        public string VerificationCode { get; set; } = null!;
    }
} 