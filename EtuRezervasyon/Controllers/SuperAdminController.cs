using EtuRezervasyon.Data;
using EtuRezervasyon.Models;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Rendering;
using Microsoft.EntityFrameworkCore;
using EtuRezervasyon.Services;

namespace EtuRezervasyon.Controllers;

    [Authorize(Roles = "Admin")]
    public class SuperAdminController : Controller
    {
        private readonly AppDbContext _context;
        private readonly NotificationService _notificationService;
        
        public SuperAdminController(AppDbContext context, NotificationService notificationService)
        {
            _context = context;
            _notificationService = notificationService;
        }
        public IActionResult Index()
        {
            var totalReservations = _context.Reservations.Count();
            var approvedReservations = _context.Reservations.Count(r => r.Status == "approved");
            var pendingReservations = _context.Reservations.Count(r => r.Status == "pending");
            var rejectedReservations = _context.Reservations.Count(r => r.Status == "rejected");

            ViewBag.TotalReservations = totalReservations;
            ViewBag.ApprovedReservations = approvedReservations;
            ViewBag.PendingReservations = pendingReservations;
            ViewBag.RejectedReservations = rejectedReservations;
            return View();
        }
        
    
        
        [HttpGet]
        public IActionResult GetAllReservations()
        {
            var reservations = _context.Reservations
                .Include(r => r.User)
                .Include(r => r.Resource)
                .Select(r => new {
                    r.Id,
                    UserFullName = r.User.FullName,
                    r.User.Email,
                    ResourceName = r.Resource.Name,
                    r.Status,
                    StartTime = r.StartTime.ToString("yyyy-MM-dd HH:mm"),
                    EndTime = r.EndTime.ToString("yyyy-MM-dd HH:mm")
                })
                .ToList();

            return Json(reservations);
        }
        public IActionResult ReservationDetails(int id)
        {
            var res = _context.Reservations
                .Include(r => r.User)
                .Include(r => r.Resource)
                .FirstOrDefault(r => r.Id == id);

            if (res == null) return NotFound();

            return View(res);
        }
        //d
        [HttpGet]
        public IActionResult EditReservation(int id)
        {
            var res = _context.Reservations.FirstOrDefault(r => r.Id == id);
            if (res == null) return NotFound();

            return View(res); // View'e model gönder
        }
        [HttpDelete]
        public IActionResult DeleteReservation(int id)
        {
            var res = _context.Reservations.Find(id);
            if (res == null) return NotFound();

            _context.Reservations.Remove(res);
            _context.SaveChanges();

            return Ok();
        }
        [HttpPost]
        public async Task<IActionResult> EditReservation(Reservation model)
        {
            var existing = await _context.Reservations.FirstOrDefaultAsync(r => r.Id == model.Id);
            if (existing == null) return NotFound();

            // Eski durumu sakla
            string oldStatus = existing.Status;

            // Değerleri güncelle
            existing.StartTime = model.StartTime;
            existing.EndTime = model.EndTime;
            existing.Status = model.Status;

            await _context.SaveChangesAsync();
            
            // Durum değiştiyse bildirim gönder
            if (oldStatus != model.Status)
            {
                Console.WriteLine($"===== REZERVASYON DURUMU DEĞİŞTİ: {oldStatus} -> {model.Status} (ID: {model.Id}) =====");
                
                try
                {
                    if (model.Status == "approved" && oldStatus != "approved")
                    {
                        Console.WriteLine($"Rezervasyon onaylandı - bildirim gönderiliyor (ID: {model.Id})");
                        await _notificationService.SendReservationApprovedNotification(model.Id, "Yönetici");
                    }
                    else if (model.Status == "rejected" && oldStatus != "rejected")
                    {
                        Console.WriteLine($"Rezervasyon reddedildi - bildirim gönderiliyor (ID: {model.Id})");
                        await _notificationService.SendReservationRejectedNotification(model.Id, "Yönetici");
                    }
                    else if (model.Status == "cancelled" && oldStatus != "cancelled")
                    {
                        Console.WriteLine($"Rezervasyon iptal edildi - bildirim gönderiliyor (ID: {model.Id})");
                        await _notificationService.SendReservationCancelledNotification(model.Id, "Yönetici");
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Bildirim gönderirken hata oluştu: {ex.Message}");
                }
            }

            return RedirectToAction("Index");
        }
        [HttpGet]
        public IActionResult GetAllUsers()
        {
            var users = _context.Users
                .Include(u => u.Role)
                .Select(u => new {
                    u.UserId,
                    u.FullName,
                    u.Email,
                    Role = u.Role != null ? u.Role.Name : "Yok",
                    CreatedAt = u.CreatedAt.ToString("yyyy-MM-dd HH:mm")
                })
                .ToList();

            return Json(users);
        }
        [HttpGet]
        public IActionResult UserDetails(int id)
        {
            var user = _context.Users
                .Include(u => u.Role)
                .FirstOrDefault(u => u.UserId == id);

            if (user == null) return NotFound();

            return View(user);
        }
        [HttpDelete]
        public IActionResult DeleteUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null) return NotFound();

            _context.Users.Remove(user);
            _context.SaveChanges();

            return Ok();
        }
        [HttpPost]
        public IActionResult EditUser(User model)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == model.UserId);
            if (user == null) return NotFound();

            user.FullName = model.FullName;
            user.Email = model.Email;
            user.RoleId = model.RoleId;

            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        [HttpGet]
        public IActionResult EditUser(int id)
        {
            var user = _context.Users.FirstOrDefault(u => u.UserId == id);
            if (user == null) return NotFound();

            ViewBag.Roles = new SelectList(_context.Roles.ToList(), "Id", "Name");
            return View(user);
        }
        
        [HttpPost]
        public IActionResult AddUser(AddUserDto model)
        {
            if (!ModelState.IsValid)
                return BadRequest("Geçersiz veri");

            // Email uzantı kontrolü
            if (!model.Email.EndsWith("@erzurum.edu.tr"))
                return BadRequest("E-posta erzurum.edu.tr ile bitmelidir.");

            // Aynı e-posta ile kullanıcı var mı kontrol et
            if (_context.Users.Any(u => u.Email == model.Email))
                return BadRequest("Bu e-posta zaten kayıtlı.");

            var user = new User
            {
                FullName = model.FullName,
                Email = model.Email,
                PasswordHash = PasswordHasher.HashPassword(model.Password),
                CreatedAt = DateTime.UtcNow,
                RoleId = 2 // örnek: 2 = normal kullanıcı
            };

            _context.Users.Add(user);
            _context.SaveChanges();

            return RedirectToAction("Index");
        }

        // POST: /SuperAdmin/CancelReservation
        [HttpPost]
        public async Task<IActionResult> CancelReservation(int id)
        {
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null) 
                return NotFound(new { error = "Rezervasyon bulunamadı" });

            // Rezervasyonu iptal et
            reservation.Status = "cancelled";
            await _context.SaveChangesAsync();
            
            // Bildirim ve e-posta gönder
            await _notificationService.SendReservationCancelledNotification(id, "Yönetici");

            return Ok(new { message = "Rezervasyon iptal edildi" });
        }

        // POST: /SuperAdmin/ApproveReservation
        [HttpPost]
        public async Task<IActionResult> ApproveReservation(int id)
        {
            Console.WriteLine($"===== REZERVASYON ONAYLAMA İSTEĞİ (ID: {id}) =====");
            
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null) 
            {
                Console.WriteLine($"HATA: Rezervasyon bulunamadı (ID: {id})");
                return NotFound(new { error = "Rezervasyon bulunamadı" });
            }

            // Rezervasyonu onayla
            reservation.Status = "approved";
            await _context.SaveChangesAsync();
            Console.WriteLine($"BAŞARILI: Rezervasyon durumu 'approved' olarak güncellendi (ID: {id})");
            
            try
            {
                // Bildirim ve e-posta gönder
                Console.WriteLine($"BİLDİRİM GÖNDERİLİYOR: SendReservationApprovedNotification çağrılıyor (ID: {id})");
                await _notificationService.SendReservationApprovedNotification(id, "Yönetici");
                Console.WriteLine($"BİLDİRİM BAŞARILI: Rezervasyon onay bildirimi gönderildi (ID: {id})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BİLDİRİM HATASI: Rezervasyon onay bildirimi gönderilemedi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"İÇ HATA: {ex.InnerException.Message}");
                }
            }

            return Ok(new { message = "Rezervasyon onaylandı" });
        }

        // POST: /SuperAdmin/RejectReservation
        [HttpPost]
        public async Task<IActionResult> RejectReservation(int id)
        {
            Console.WriteLine($"===== REZERVASYON RED İSTEĞİ (ID: {id}) =====");
            
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null) 
            {
                Console.WriteLine($"HATA: Rezervasyon bulunamadı (ID: {id})");
                return NotFound(new { error = "Rezervasyon bulunamadı" });
            }

            // Rezervasyonu reddet
            reservation.Status = "rejected";
            await _context.SaveChangesAsync();
            Console.WriteLine($"BAŞARILI: Rezervasyon durumu 'rejected' olarak güncellendi (ID: {id})");
            
            try
            {
                // Bildirim ve e-posta gönder
                Console.WriteLine($"BİLDİRİM GÖNDERİLİYOR: SendReservationRejectedNotification çağrılıyor (ID: {id})");
                await _notificationService.SendReservationRejectedNotification(id, "Yönetici");
                Console.WriteLine($"BİLDİRİM BAŞARILI: Rezervasyon red bildirimi gönderildi (ID: {id})");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"BİLDİRİM HATASI: Rezervasyon red bildirimi gönderilemedi: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"İÇ HATA: {ex.InnerException.Message}");
                }
            }

            return Ok(new { message = "Rezervasyon reddedildi" });
        }

        // GET: /SuperAdmin/GetReservationStatistics
        [HttpGet]
        public IActionResult GetReservationStatistics()
        {
            // Son 30 gündeki rezervasyon sayısı
            var startDate = DateTime.Now.AddDays(-30);
            var endDate = DateTime.Now;

            var statistics = new
            {
                TotalReservations = _context.Reservations.Count(),
                PendingReservations = _context.Reservations.Count(r => r.Status == "pending"),
                ApprovedReservations = _context.Reservations.Count(r => r.Status == "approved"),
                RejectedReservations = _context.Reservations.Count(r => r.Status == "rejected"),
                CancelledReservations = _context.Reservations.Count(r => r.Status == "cancelled"),
                Last30DaysReservations = _context.Reservations.Count(r => r.CreatedAt >= startDate && r.CreatedAt <= endDate),
                ByResourceType = new
                {
                    Library = _context.Reservations
                        .Count(r => r.Resource.Type.ToLower() == "library" && r.Status != "cancelled" && r.Status != "rejected"),
                    Room = _context.Reservations
                        .Count(r => r.Resource.Type.ToLower() == "room" && r.Status != "cancelled" && r.Status != "rejected"),
                    Conference = _context.Reservations
                        .Count(r => r.Resource.Type.ToLower() == "conference" && r.Status != "cancelled" && r.Status != "rejected")
                }
            };

            return Ok(statistics);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<IActionResult> UpdateUserPassword([FromBody] ChangePasswordDto model)
        {
            try
            {
                var user = await _context.Users.FindAsync(model.UserId);
                if (user == null)
                    return NotFound(new { error = "Kullanıcı bulunamadı" });

                // Şifreyi hashle ve güncelle
                user.PasswordHash = PasswordHasher.HashPassword(model.NewPassword);
                await _context.SaveChangesAsync();

                return Ok(new { message = "Şifre başarıyla güncellendi" });
            }
            catch (Exception ex)
            {
                return BadRequest(new { error = "Şifre güncellenirken bir hata oluştu: " + ex.Message });
            }
        }
    }

