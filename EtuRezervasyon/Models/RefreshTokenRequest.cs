using System.ComponentModel.DataAnnotations;

namespace EtuRezervasyon.Models
{
    public class RefreshTokenRequest
    {
        [Required(ErrorMessage = "Email adresi gereklidir")]
        [EmailAddress(ErrorMessage = "Geçerli bir email adresi giriniz")]
        public string Email { get; set; } = null!;
    }
} 