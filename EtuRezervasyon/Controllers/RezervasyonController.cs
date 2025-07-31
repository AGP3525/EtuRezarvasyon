using Microsoft.AspNetCore.Mvc;
using EtuRezervasyon.Data;
using EtuRezervasyon.Models;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using Microsoft.AspNetCore.Authorization;
using EtuRezervasyon.Services;

namespace EtuRezervasyon.Controllers;

public class RezervasyonController : Controller
{
    private readonly AppDbContext _context;
    private readonly NotificationService _notificationService;

    public RezervasyonController(AppDbContext context, NotificationService notificationService) 
    {
        _context = context;
        _notificationService = notificationService;
    }

    // GET
    public IActionResult Index(string? resourceType = null)
    {
        // resourceType parametresi varsa onu kullan, yoksa varsayılan olarak "room" kullan
        if (string.IsNullOrEmpty(resourceType))
        {
            resourceType = "room";
        }
        
        ViewBag.ResourceType = resourceType;
        ViewBag.Title = resourceType switch
        {
            "library" => "Kütüphane Rezervasyonları",
            "room" => "Proje Odası Rezervasyonları",
            "conference" => "Konferans Salonu Rezervasyonları",
            _ => "Rezervasyonlar"
        };
        
        return View();
    }
    
    // GET: /Rezarvasyon/Kütüphane
    public IActionResult Kütüphane()
    {
        ViewBag.Title = "Kütüphane Rezervasyonları";
        ViewBag.ResourceType = "library";
        return View("Index");
    }
    
    // GET: /Rezarvasyon/ProjeOdasi
    public IActionResult ProjeOdasi()
    {
        ViewBag.Title = "Proje Odası Rezervasyonları";
        ViewBag.ResourceType = "room";
        return View("Index");
    }
    
    // GET: /Rezarvasyon/KonferansSalonu
    public IActionResult KonferansSalonu()
    {
        ViewBag.Title = "Konferans Salonu Rezervasyonları";
        ViewBag.ResourceType = "conference";
        return View("Index");
    }

    // GET: /Rezervasyon/List
    [Authorize]
    public async Task<IActionResult> List()
    {
        var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
        if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
        {
            // Kullanıcı kimliği yoksa veya geçersizse giriş sayfasına yönlendir
            // AccountController ve Login action'ınızın adına göre güncelleyin
            return RedirectToAction("Index", "Signin"); 
        }

        var reservations = await _context.Reservations
            .Where(r => r.UserId == userId)
            .Include(r => r.Resource) // Kaynak bilgilerini de dahil et
            .OrderByDescending(r => r.StartTime) // En yeni rezervasyonlar üstte
            .ToListAsync();

        return View(reservations);
    }
    
    // API Endpoints

    // GET: /Rezervasyon/GetAvailableHours
    [HttpGet]
    public async Task<IActionResult> GetAvailableHours(string date, string resourceType)
    {
        try
        {
            if (string.IsNullOrEmpty(date) || string.IsNullOrEmpty(resourceType))
            {
                return BadRequest(new { error = "Tarih ve kaynak tipi gereklidir." });
            }

            // String tarihi DateTime'a çevir
            if (!DateTime.TryParse(date, out DateTime selectedDate))
            {
                return BadRequest(new { error = "Geçersiz tarih formatı." });
            }

            // UTC DateTime formatını Local DateTime formatına dönüştür - saatleri doğru göstermek için
            selectedDate = DateTime.SpecifyKind(selectedDate.Date, DateTimeKind.Utc);
            
            Console.WriteLine($"İstek tarihi: {date}, Dönüştürülen tarih: {selectedDate:yyyy-MM-dd}");
            
            // Seçilen kaynağı (resource) bul - tip eşleşmesini küçük harfle karşılaştır
            var resources = await _context.Resources
                .Where(r => r.Type.ToLower() == resourceType.ToLower())
                .ToListAsync();

            if (resources == null || !resources.Any())
            {
                // Eğer kaynak bulunamadıysa, yeni sabit saatler ve boş sonuç döndür
                var defaultTimeSlots = new List<string>
                {
                    "10:00 - 11:00",
                    "11:00 - 12:00",
                    "12:00 - 13:00",
                    "13:00 - 14:00",
                    "14:00 - 15:00",
                    "15:00 - 16:00"
                };

                var defaultResult = defaultTimeSlots.Select(slot => new TimeSlotInfo 
                { 
                    TimeSlot = slot, 
                    IsReserved = false 
                }).ToList();

                return Ok(defaultResult);
            }

            // Saat aralıkları
            var timeSlots = new List<string>
            {
                "10:00 - 11:00",
                "11:00 - 12:00",
                "12:00 - 13:00",
                "13:00 - 14:00",
                "14:00 - 15:00",
                "15:00 - 16:00"
            };

            // Seçilen gün için tüm rezervasyonları bul
            var resourceIds = resources.Select(r => r.Id).ToList();
            Console.WriteLine($"Seçilen tarih: {selectedDate:yyyy-MM-dd}");
            Console.WriteLine($"Kaynak ID'leri: {string.Join(", ", resourceIds)}");
            
            // SORGU: Seçilen tarih ve kaynaklara ait tüm rezervasyonları al
            var allReservationsOnDate = await _context.Reservations
                .Where(r => resourceIds.Contains(r.ResourceId) && 
                       r.StartTime.Date == selectedDate.Date)
                .ToListAsync();
                
            Console.WriteLine($"Toplam rezervasyon sayısı: {allReservationsOnDate.Count}");
            
            // TÜM rezervasyonları debug için konsola yazdır
            foreach (var res in allReservationsOnDate)
            {
                Console.WriteLine($"RES: ID={res.Id}, ResourceID={res.ResourceId}, " +
                                 $"Start={res.StartTime:yyyy-MM-dd HH:mm}, End={res.EndTime:yyyy-MM-dd HH:mm}, " +
                                 $"Status={res.Status}");
            }

            // Sonuç oluştur - tüm saat aralıkları için
            var result = new List<TimeSlotInfo>();
            
            foreach (var timeSlot in timeSlots)
            {
                // Zaman aralığını al (10:00 - 11:00 formatından)
                var timeParts = timeSlot.Split(" - ");
                var startTimeStr = timeParts[0];
                var endTimeStr = timeParts[1];
                
                // TimeSpan'a çevir
                TimeSpan startTimeSpan = TimeSpan.Parse(startTimeStr);
                TimeSpan endTimeSpan = TimeSpan.Parse(endTimeStr);
                
                // Tam tarihe dönüştür
                DateTime slotStartTime = selectedDate.Add(startTimeSpan);
                DateTime slotEndTime = selectedDate.Add(endTimeSpan);
                
                Console.WriteLine($"Kontrol edilen saat: {timeSlot}, UTC StartTime: {slotStartTime:yyyy-MM-dd HH:mm}");
                
                // Bu saat aralığında aktif rezervasyon var mı kontrol et (status != cancelled)
                bool isReserved = allReservationsOnDate.Any(r => 
                    r.StartTime == slotStartTime && 
                    r.EndTime == slotEndTime && 
                    r.Status != "cancelled" && 
                    r.Status != "rejected");
                
                // Bu zaman aralığı için rezervasyon varsa ID'sini al, yoksa null olsun
                int? reservationId = null;
                if (isReserved)
                {
                    var reservation = allReservationsOnDate.FirstOrDefault(r => 
                        r.StartTime == slotStartTime && 
                        r.EndTime == slotEndTime && 
                        r.Status != "cancelled" && 
                        r.Status != "rejected");
                        
                    if (reservation != null)
                    {
                        reservationId = reservation.Id;
                    }
                }
                
                Console.WriteLine($"Saat: {timeSlot}, Rezerve: {isReserved}, ID: {reservationId}");
                
                // Sonuca ekle
                result.Add(new TimeSlotInfo
                {
                    TimeSlot = timeSlot,
                    IsReserved = isReserved,
                    ReservationId = reservationId
                });
            }
            
            Console.WriteLine($"Toplam sonuç adet: {result.Count}, Rezerve saatler: {result.Count(r => r.IsReserved)}");
            
            return Ok(result);
        }
        catch (Exception ex)
        {
            // Hata günlüğe kaydet
            Console.WriteLine($"GetAvailableHours metodu hata: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            
            // Sabit bir saatler listesi döndür - varsayılan olarak hiçbiri rezerve değil
            var fallbackTimeSlots = new List<string>
            {
                "10:00 - 11:00",
                "11:00 - 12:00",
                "12:00 - 13:00",
                "13:00 - 14:00",
                "14:00 - 15:00",
                "15:00 - 16:00"
            };

            var fallbackResult = fallbackTimeSlots.Select(slot => new TimeSlotInfo 
            { 
                TimeSlot = slot, 
                IsReserved = false 
            }).ToList();

            // Hata olsa bile kullanılabilir bir yanıt döndür
            return Ok(fallbackResult);
        }
    }

    // POST: /Rezervasyon/CreateReservation
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CreateReservation([FromBody] CreateReservationRequest request)
    {
        try 
        {
            // Tüm istek verilerini günlüğe yaz
            Console.WriteLine("=== CreateReservation API Çağrısı ===");
            Console.WriteLine($"İstek: {(request != null ? "Dolu" : "NULL")}");
            
            if (request != null)
            {
                Console.WriteLine($"Date: '{request.Date}'");
                Console.WriteLine($"TimeSlot: '{request.TimeSlot}'");
                Console.WriteLine($"ResourceType: '{request.ResourceType}'");
            }
            
            // Null kontrolü yap
            if (request == null)
            {
                Console.WriteLine("HATA: İstek verisi (request) null!");
                return BadRequest(new { error = "İstek verisi boş." });
            }
            
            // Gerekli alanları kontrol et
            if (string.IsNullOrEmpty(request.Date))
            {
                Console.WriteLine("HATA: Date alanı boş veya null!");
                return BadRequest(new { error = "Tarih alanı gereklidir." });
            }
            
            if (string.IsNullOrEmpty(request.TimeSlot))
            {
                Console.WriteLine("HATA: TimeSlot alanı boş veya null!");
                return BadRequest(new { error = "Saat aralığı alanı gereklidir." });
            }
            
            if (string.IsNullOrEmpty(request.ResourceType))
            {
                Console.WriteLine("HATA: ResourceType alanı boş veya null!");
                return BadRequest(new { error = "Kaynak tipi alanı gereklidir." });
            }

            // String tarihi DateTime'a çevir
            if (!DateTime.TryParse(request.Date, out DateTime reservationDate))
            {
                Console.WriteLine($"HATA: Geçersiz tarih formatı: '{request.Date}'");
                return BadRequest(new { error = "Geçersiz tarih formatı." });
            }

            // Kullanıcı kimliğini al ve doğrula
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            Console.WriteLine($"UserIdClaim: {(userIdClaim != null ? userIdClaim.Value : "NULL")}");
            
            if (userIdClaim == null || string.IsNullOrEmpty(userIdClaim.Value))
            {
                Console.WriteLine("HATA: Kullanıcı kimliği bulunamadı.");
                return Unauthorized(new { error = "Kullanıcı kimliği bulunamadı." });
            }
            
            // UserID'nin integer'a çevrilmesini sağla
            if (!int.TryParse(userIdClaim.Value, out int userIdInt))
            {
                Console.WriteLine($"HATA: Kullanıcı kimliği integer'a çevrilemedi: {userIdClaim.Value}");
                return Unauthorized(new { error = "Kullanıcı kimliği geçersiz." });
            }
            Console.WriteLine($"Çevrilen UserId: {userIdInt}");
            
            // Kullanıcının varlığını doğrula
            var userExists = await _context.Users.AnyAsync(u => u.UserId == userIdInt);
            if (!userExists)
            {
                Console.WriteLine($"HATA: {userIdInt} ID'li kullanıcı veritabanında bulunamadı.");
                return Unauthorized(new { error = "Kullanıcı bulunamadı." });
            }
            Console.WriteLine($"Kullanıcı bulundu: ID={userIdInt}");
            
            // Kaynak tipine göre resource ID'yi bul
            var resource = await _context.Resources
                .FirstOrDefaultAsync(r => r.Type.ToLower() == request.ResourceType.ToLower());

            if (resource == null)
            {
                Console.WriteLine($"UYARI: '{request.ResourceType}' tipinde kaynak bulunamadı. Otomatik olarak oluşturulacak.");
                
                // Veritabanındaki tüm kaynakları listele
                var allResources = await _context.Resources.ToListAsync();
                Console.WriteLine($"Mevcut kaynaklar: {string.Join(", ", allResources.Select(r => $"{r.Name} ({r.Type})"))}");
                
                // Kaynak tipine göre yeni bir kaynak oluştur
                var newResourceName = request.ResourceType.ToLower() switch
                {
                    "library" => "Kütüphane",
                    "room" => "Proje Odası",
                    "conference" => "Konferans Salonu",
                    _ => $"{char.ToUpper(request.ResourceType[0])}{request.ResourceType.Substring(1)} Kaynağı"
                };
                
                var newResourceLocation = request.ResourceType.ToLower() switch
                {
                    "library" => "Ana Bina",
                    "room" => "C Blok",
                    "conference" => "B Blok",
                    _ => "Kampüs"
                };
                
                var newResource = new Resource
                {
                    Name = newResourceName,
                    Type = request.ResourceType.ToLower(),
                    Capacity = request.ResourceType.ToLower() switch
                    {
                        "library" => 100,
                        "room" => 10,
                        "conference" => 200,
                        _ => 20
                    },
                    Location = newResourceLocation,
                    CreatedAt = DateTime.UtcNow
                };
                
                _context.Resources.Add(newResource);
                await _context.SaveChangesAsync();
                
                Console.WriteLine($"YENİ KAYNAK OLUŞTURULDU: ID={newResource.Id}, Name={newResource.Name}, Type={newResource.Type}");
                
                // Yeni oluşturulan kaynağı kullan
                resource = newResource;
            }

            // Saat aralığını parse et (örn: "10:00 - 11:00")
            var timeSlotParts = request.TimeSlot.Split(" - ");
            if (timeSlotParts.Length != 2)
            {
                Console.WriteLine($"HATA: Geçersiz saat formatı: '{request.TimeSlot}'");
                return BadRequest(new { error = "Geçersiz saat formatı." });
            }

            // Saatleri ayır ve DateTime'a çevir
            TimeSpan startTimeSpan, endTimeSpan;
            if (!TimeSpan.TryParse(timeSlotParts[0], out startTimeSpan) || !TimeSpan.TryParse(timeSlotParts[1], out endTimeSpan))
            {
                Console.WriteLine($"HATA: Saatler ayrıştırılamadı: '{timeSlotParts[0]}' veya '{timeSlotParts[1]}'");
                return BadRequest(new { error = $"Geçersiz saat formatı. Gönderilen: {request.TimeSlot}" });
            }
            
            // Tarih kısmını Date ile alarak sadece gün kısmını koruyoruz
            var dateOnly = reservationDate.Date;
            
            // Tarihi UTC formatında oluştur
            var startTime = DateTime.SpecifyKind(dateOnly.Add(startTimeSpan), DateTimeKind.Utc);
            var endTime = DateTime.SpecifyKind(dateOnly.Add(endTimeSpan), DateTimeKind.Utc);
            
            Console.WriteLine($"Rezervasyon tarihi: {reservationDate.Date:yyyy-MM-dd}");
            Console.WriteLine($"Başlangıç saati (UTC): {startTime:yyyy-MM-dd HH:mm}");
            Console.WriteLine($"Bitiş saati (UTC): {endTime:yyyy-MM-dd HH:mm}");

            // Bu saat aralığında zaten rezervasyon var mı kontrol et
            var existingReservation = await _context.Reservations
                .AnyAsync(r => r.ResourceId == resource.Id && 
                         r.StartTime == startTime && 
                         r.EndTime == endTime &&
                         r.Status != "cancelled" &&
                         r.Status != "rejected");

            if (existingReservation)
            {
                Console.WriteLine("HATA: Bu saat aralığı zaten rezerve edilmiş.");
                return BadRequest(new { error = "Bu saat aralığı zaten rezerve edilmiş." });
            }

            // Mevcut iptal edilmiş veya reddedilmiş rezervasyonu kontrol et - varsa güncelle
            var cancelledReservation = await _context.Reservations
                .FirstOrDefaultAsync(r => r.ResourceId == resource.Id && 
                                    r.StartTime == startTime && 
                                    r.EndTime == endTime && 
                                    (r.Status == "cancelled" || r.Status == "rejected"));
                                    
            if (cancelledReservation != null)
            {
                // İptal edilmiş rezervasyonu güncelle
                Console.WriteLine($"İptal edilmiş rezervasyon bulundu (ID: {cancelledReservation.Id}). Güncelleniyor...");
                cancelledReservation.UserId = userIdInt;
                cancelledReservation.Status = "pending";
                cancelledReservation.CreatedAt = DateTime.UtcNow;
                
                try {
                    await _context.SaveChangesAsync();
                    Console.WriteLine($"BAŞARILI: İptal edilmiş rezervasyon güncellendi: ID={cancelledReservation.Id}");
                    
                    // Bildirim ve e-posta gönder - iptal edilmiş/reddedilmiş rezervasyon tekrar aktifleştirildiğinde
                    await _notificationService.SendReservationCreatedNotification(cancelledReservation.Id);
                    Console.WriteLine($"BİLDİRİM GÖNDERİLDİ: Yeniden aktifleştirilen rezervasyon için bildirim gönderildi (ID: {cancelledReservation.Id})");
                    
                    return Ok(new { message = "Rezervasyon başarıyla oluşturuldu.", reservationId = cancelledReservation.Id });
                }
                catch (Exception innerEx) {
                    Console.WriteLine($"HATA: İptal edilmiş rezervasyon güncellenirken hata: {innerEx.Message}");
                    Console.WriteLine($"Inner exception: {innerEx.InnerException?.Message}");
                    return StatusCode(500, new { error = "Rezervasyon güncellenirken hata oluştu.", details = innerEx.Message, innerMessage = innerEx.InnerException?.Message });
                }
            }

            // Yeni rezervasyon oluştur
            var reservation = new Reservation
            {
                UserId = userIdInt,
                ResourceId = resource.Id,
                StartTime = startTime,
                EndTime = endTime,
                Status = "pending", // Varsayılan durum: beklemede
                CreatedAt = DateTime.UtcNow // DateTime.Now yerine UtcNow kullan
            };

            Console.WriteLine($"Oluşturulacak rezervasyon: UserID={reservation.UserId}, ResourceID={reservation.ResourceId}, " +
                              $"StartTime={reservation.StartTime}, EndTime={reservation.EndTime}");
            
            try {
                _context.Reservations.Add(reservation);
                await _context.SaveChangesAsync();
                
                Console.WriteLine($"BAŞARILI: Yeni rezervasyon oluşturuldu: ID={reservation.Id}, User={userIdInt}, Resource={resource.Name}, StartTime={startTime}");
                
                // Bildirim ve e-posta gönder
                await _notificationService.SendReservationCreatedNotification(reservation.Id);
                
                return Ok(new { message = "Rezervasyon başarıyla oluşturuldu.", reservationId = reservation.Id });
            }
            catch (DbUpdateException dbEx) {
                Console.WriteLine($"HATA: Veritabanı güncelleme hatası: {dbEx.Message}");
                Console.WriteLine($"Inner exception: {dbEx.InnerException?.Message}");
                
                // Bağlantı durumunu kontrol et
                Console.WriteLine("Veritabanı bağlantı kontrolü yapılıyor...");
                try {
                    bool canConnect = await _context.Database.CanConnectAsync();
                    Console.WriteLine($"Veritabanına bağlanılabilir mi: {canConnect}");
                } catch (Exception connEx) {
                    Console.WriteLine($"Bağlantı kontrolü sırasında hata: {connEx.Message}");
                }
                
                // Denenecek alternatif çözüm: Yeni context ile
                try {
                    Console.WriteLine("Rezervasyonu yeni bir veritabanı bağlantısı ile kaydetmeyi deniyorum...");
                    
                    // Değişiklikleri geri al
                    foreach (var entry in _context.ChangeTracker.Entries())
                    {
                        entry.State = EntityState.Detached;
                    }
                    
                    // Yeni bir rezervasyon oluştur
                    var newReservation = new Reservation
                    {
                        UserId = userIdInt,
                        ResourceId = resource.Id,
                        StartTime = startTime,
                        EndTime = endTime,
                        Status = "pending",
                        CreatedAt = DateTime.UtcNow // DateTime.Now yerine UtcNow kullan
                    };
                    
                    _context.Reservations.Add(newReservation);
                    await _context.SaveChangesAsync();
                    
                    Console.WriteLine($"BAŞARILI: Alternatif yöntemle rezervasyon oluşturuldu: ID={newReservation.Id}");
                    return Ok(new { message = "Rezervasyon başarıyla oluşturuldu.", reservationId = newReservation.Id });
                }
                catch (Exception altEx) {
                    Console.WriteLine($"Alternatif kayıt denemesi de başarısız: {altEx.Message}");
                }
                
                return StatusCode(500, new { error = "Rezervasyon kaydı sırasında veritabanı hatası oluştu.", details = dbEx.Message, innerMessage = dbEx.InnerException?.Message });
            }
            catch (Exception innerEx) {
                Console.WriteLine($"HATA: SaveChangesAsync sırasında hata oluştu: {innerEx.Message}");
                Console.WriteLine($"Inner exception: {innerEx.InnerException?.Message}");
                return StatusCode(500, new { error = "Rezervasyon veritabanına kaydedilirken hata oluştu.", details = innerEx.Message, innerMessage = innerEx.InnerException?.Message });
            }
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HATA OLUŞTU: CreateReservation metodu hata: {ex.Message}");
            Console.WriteLine($"Stack trace: {ex.StackTrace}");
            Console.WriteLine($"Inner exception: {ex.InnerException?.Message}");
            return StatusCode(500, new { error = "Rezervasyon oluşturulurken bir hata oluştu.", details = ex.Message, innerMessage = ex.InnerException?.Message });
        }
    }

    // Rezervasyon iptal etme metodu
    [HttpPost]
    [Authorize]
    [ValidateAntiForgeryToken]
    public async Task<IActionResult> CancelReservation(int id)
    {
        try
        {
            // Kullanıcı kimliğini al
            var userIdClaim = User.FindFirst(ClaimTypes.NameIdentifier);
            if (userIdClaim == null || !int.TryParse(userIdClaim.Value, out int userId))
            {
                return Unauthorized(new { error = "Kullanıcı kimliği doğrulanamadı." });
            }
            
            // Rezervasyonu bul
            var reservation = await _context.Reservations.FindAsync(id);
            if (reservation == null)
            {
                return NotFound(new { error = "Rezervasyon bulunamadı." });
            }
            
            // Yalnızca kendi rezervasyonlarını veya admin kullanıcı iptal edebilir
            bool isAdmin = User.IsInRole("Admin"); // Admin rol kontrolü
            if (reservation.UserId != userId && !isAdmin)
            {
                return StatusCode(403, new { error = "Bu rezervasyonu iptal etme yetkiniz yok." });
            }

            if (reservation.Status == "cancelled")
            {
                return BadRequest(new { error = "Bu rezervasyon zaten iptal edilmiş." });
            }
            
            // Rezervasyonu iptal et
            reservation.Status = "cancelled";
            await _context.SaveChangesAsync();
            
            // Bildirim ve e-posta gönder (eğer kullanıcı kendi iptal ediyorsa)
            // Eğer admin iptal ediyorsa, bu zaten SuperAdminController'da yönetiliyor olabilir.
            // Bu senaryoda, işlemi yapanın "user" olduğunu belirtiyoruz.
            await _notificationService.SendReservationCancelledNotification(id, User.Identity?.Name ?? "Kullanıcı");
            
            return Ok(new { message = "Rezervasyon başarıyla iptal edildi." });
        }
        catch (Exception ex)
        {
            Console.WriteLine($"HATA: Rezervasyon iptal edilirken hata oluştu: {ex.Message}");
            return StatusCode(500, new { error = "Rezervasyon iptal edilirken bir hata oluştu.", details = ex.Message });
        }
    }

    // Önceki metodlar için yönlendirmeler
    // GET: /Rezarvasyon/Library
    public IActionResult Library()
    {
        return RedirectToAction("Kütüphane");
    }
    
    // GET: /Rezarvasyon/ConferenceHall
    public IActionResult ConferenceHall()
    {
        return RedirectToAction("KonferansSalonu");
    }
    
    // Calendar kontrolcüsünden aktarılan işlevsellik
    // Bu eski URL formatını desteklemek için (/Calendar?resourceType=...)
    public IActionResult Calendar(string resourceType)
    {
        return RedirectToAction("Index", new { resourceType });
    }
    
    // GET: /Rezarvasyon/ÇalışmaOdası
    public IActionResult ÇalışmaOdası()
    {
        return RedirectToAction("ProjeOdasi");
    }
}

public class TimeSlotInfo
{
    public string TimeSlot { get; set; } = string.Empty;
    public bool IsReserved { get; set; }
    public int? ReservationId { get; set; }
}

public class CreateReservationRequest
{
    public string Date { get; set; } = string.Empty;
    public string TimeSlot { get; set; } = string.Empty;
    public string ResourceType { get; set; } = string.Empty;
}