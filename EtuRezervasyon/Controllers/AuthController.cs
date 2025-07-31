using Microsoft.AspNetCore.Mvc;
using EtuRezervasyon.Data;
using EtuRezervasyon.Models;
using EtuRezervasyon.Services;
using System.Threading.Tasks;

namespace EtuRezervasyon.Controllers
{
    [Route("api/[controller]")]
    [ApiController]
    public class AuthController : ControllerBase
    {
        private readonly AuthService _authService;

        public AuthController(AuthService authService)
        {
            _authService = authService;
        }

        [HttpPost("login")]
        public async Task<IActionResult> Login([FromBody] LoginRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (user, token, errorMessage) = await _authService.AuthenticateAsync(request.Email, request.Password);

            if (user == null || token == null)
            {
                return Unauthorized(new { message = errorMessage });
            }

            // Token dön
            return Ok(new
            {
                token = token,
                user = new
                {
                    id = user.UserId,
                    email = user.Email,
                    fullName = user.FullName,
                    role = user.Role?.Name
                }
            });
        }

        [HttpPost("refresh-token")]
        public async Task<IActionResult> RefreshToken([FromBody] RefreshTokenRequest request)
        {
            if (!ModelState.IsValid)
            {
                return BadRequest(ModelState);
            }

            var (token, errorMessage) = await _authService.RefreshTokenAsync(request.Email);

            if (token == null)
            {
                return Unauthorized(new { message = errorMessage });
            }

            // Token dön
            return Ok(new
            {
                token = token
            });
        }
    }
}
