using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using System.Security.Claims;

namespace EtuRezervasyon.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class TestJwtController : ControllerBase
    {
        // Herkesin erişebileceği public bir endpoint
        [HttpGet("public")]
        public IActionResult PublicEndpoint()
        {
            return Ok(new { message = "Bu endpoint herkese açık!" });
        }

        // Sadece kimlik doğrulaması yapmış kullanıcıların erişebileceği endpoint
        [HttpGet("secured")]
        [Authorize]
        public IActionResult SecuredEndpoint()
        {
            var userId = User.FindFirstValue(ClaimTypes.NameIdentifier);
            var userName = User.FindFirstValue(ClaimTypes.Name);
            var userEmail = User.FindFirstValue(ClaimTypes.Email);
            var userRole = User.FindFirstValue(ClaimTypes.Role);

            return Ok(new { 
                message = "JWT kimlik doğrulama başarılı!",
                userId = userId,
                name = userName,
                email = userEmail,
                role = userRole
            });
        }

        // Sadece Admin rolündeki kullanıcıların erişebileceği endpoint
        [HttpGet("admin")]
        [Authorize(Roles = "Admin,SuperAdmin")]
        public IActionResult AdminEndpoint()
        {
            return Ok(new { message = "Admin yetkisi ile başarıyla erişildi!" });
        }
    }
}