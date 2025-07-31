using Microsoft.EntityFrameworkCore;
using EtuRezervasyon.Data;
using EtuRezervasyon.Models;
using EtuRezervasyon.Services;
using System.Threading.Tasks;

namespace EtuRezervasyon.Services
{
    public class AuthService
    {
        private readonly AppDbContext _context;
        private readonly TokenService _tokenService;

        public AuthService(AppDbContext context, TokenService tokenService)
        {
            _context = context;
            _tokenService = tokenService;
        }

        public async Task<(User? user, string? token, string? errorMessage)> AuthenticateAsync(string email, string password)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                return (null, null, "Geçersiz email veya şifre");
            }

            // Şifre kontrolü - gerçek uygulamada şifre hash'leri karşılaştırılmalı
            if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
            {
                return (null, null, "Geçersiz email veya şifre");
            }

            var token = _tokenService.GenerateJwtToken(user);
            return (user, token, null);
        }

        public async Task<(string? token, string? errorMessage)> RefreshTokenAsync(string email)
        {
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email);

            if (user == null)
            {
                return (null, "Kullanıcı bulunamadı");
            }

            var token = _tokenService.GenerateJwtToken(user);
            return (token, null);
        }
    }
} 