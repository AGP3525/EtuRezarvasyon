using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Authorization;
using EtuRezervasyon.Data;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;

namespace EtuRezervasyon.Controllers
{
    [Authorize]
    public class NotificationsController : Controller
    {
        private readonly AppDbContext _context;

        public NotificationsController(AppDbContext context)
        {
            _context = context;
        }

        public async Task<IActionResult> Index()
        {
            // Kullanıcı kimliğini al
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized();
            }

            // Kullanıcının bildirimlerini al
            var notifications = await _context.Notifications
                .Where(n => n.UserId == userId)
                .OrderByDescending(n => n.SentAt)
                .ToListAsync();

            return View(notifications);
        }

        // İsteğe bağlı: Bildirimi okundu olarak işaretleme (ek özellik ekleyebilirsiniz)
        [HttpPost]
        public async Task<IActionResult> MarkAsRead(int id)
        {
            // Kullanıcı kimliğini al
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized();
            }

            // Bildirimi bul
            var notification = await _context.Notifications
                .FirstOrDefaultAsync(n => n.Id == id && n.UserId == userId);

            if (notification == null)
            {
                return NotFound();
            }

            // Okundu olarak işaretle (Notification modeline IsRead property'si eklenirse)
            // notification.IsRead = true;
            // await _context.SaveChangesAsync();

            return Ok();
        }
    }
} 