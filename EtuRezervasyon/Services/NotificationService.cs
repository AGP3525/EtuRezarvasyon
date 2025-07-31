using Microsoft.Extensions.Logging;
using System.Net;
using System.Net.Mail;
using EtuRezervasyon.Data;
using Microsoft.EntityFrameworkCore;
using EtuRezervasyon.Models;

namespace EtuRezervasyon.Services
{
    public class NotificationService
    {
        private readonly AppDbContext _context;
        private readonly ILogger<NotificationService> _logger;
        private readonly IConfiguration _configuration;

        public NotificationService(AppDbContext context, ILogger<NotificationService> logger, IConfiguration configuration)
        {
            _context = context;
            _logger = logger;
            _configuration = configuration;
        }

        /// <summary>
        /// E-posta gönderir
        /// </summary>
        public async Task<bool> SendEmail(string toEmail, string subject, string message)
        {
            try
            {
                // SMTP ayarlarını yapılandırmadan alın
                string smtpServer = _configuration["Email:SmtpServer"] ?? string.Empty;
                int smtpPort = int.TryParse(_configuration["Email:SmtpPort"], out int port) ? port : 587;
                string smtpUsername = _configuration["Email:Username"] ?? string.Empty;
                string smtpPassword = _configuration["Email:Password"] ?? string.Empty;
                string fromEmail = _configuration["Email:FromEmail"] ?? string.Empty;
                string fromName = _configuration["Email:FromName"] ?? string.Empty;

                // E-posta ayarlarını kontrol et ve konsola yazdır
                Console.WriteLine("==================== E-POSTA GÖNDERİM BİLGİLERİ ====================");
                Console.WriteLine($"SMTP Server: {smtpServer}");
                Console.WriteLine($"SMTP Port: {smtpPort}");
                Console.WriteLine($"Username: {smtpUsername}");
                Console.WriteLine($"Password: {(string.IsNullOrEmpty(smtpPassword) ? "BOŞ!" : "****")}");
                Console.WriteLine($"From Email: {fromEmail}");
                Console.WriteLine($"From Name: {fromName}");
                Console.WriteLine($"To Email: {toEmail}");
                Console.WriteLine($"Subject: {subject}");
                Console.WriteLine("====================================================================");

                if (string.IsNullOrEmpty(smtpServer) || string.IsNullOrEmpty(smtpUsername) || 
                    string.IsNullOrEmpty(smtpPassword) || string.IsNullOrEmpty(fromEmail))
                {
                    _logger.LogError("E-posta ayarları eksik. Lütfen appsettings.json dosyasını kontrol edin.");
                    Console.WriteLine("HATA: E-posta ayarları eksik! Mail gönderimi iptal edildi.");
                    return false;
                }

                // Debug bilgisi yazdır
                _logger.LogInformation($"Email gönderiliyor: Server={smtpServer}, Port={smtpPort}, From={fromEmail}, To={toEmail}");

                try
                {
                    // SMTP istemcisi oluştur
                    using var client = new SmtpClient(smtpServer, smtpPort)
                    {
                        Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                        EnableSsl = true,
                        DeliveryMethod = SmtpDeliveryMethod.Network,
                        Timeout = 30000 // 30 saniye timeout
                    };

                    Console.WriteLine("SMTP istemcisi oluşturuldu.");

                    // E-posta oluştur
                    using var mailMessage = new MailMessage
                    {
                        From = new MailAddress(fromEmail, fromName),
                        Subject = subject,
                        Body = message,
                        IsBodyHtml = true // HTML içerik için
                    };

                    mailMessage.To.Add(toEmail);
                    Console.WriteLine($"Mail mesajı hazırlandı. Alıcı: {toEmail}");

                    // E-postayı asenkron olarak gönder
                    Console.WriteLine("Mail gönderiliyor...");
                    await client.SendMailAsync(mailMessage);
                    Console.WriteLine("E-posta başarıyla gönderildi!");
                    _logger.LogInformation($"E-posta başarıyla gönderildi. Alıcı: {toEmail}, Konu: {subject}");
                    return true;
                }
                catch (SmtpException smtpEx)
                {
                    Console.WriteLine($"SMTP HATASI: {smtpEx.Message}");
                    Console.WriteLine($"StatusCode: {smtpEx.StatusCode}");
                    _logger.LogError($"SMTP Hatası: {smtpEx.Message}, StatusCode: {smtpEx.StatusCode}");
                    if (smtpEx.InnerException != null)
                    {
                        Console.WriteLine($"İç Hata: {smtpEx.InnerException.Message}");
                        Console.WriteLine($"İç Hata Tür: {smtpEx.InnerException.GetType().Name}");
                        _logger.LogError($"SMTP iç hata: {smtpEx.InnerException.Message}");
                    }
                    return false;
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"GENEL HATA: E-posta gönderilirken hata oluştu: {ex.Message}");
                Console.WriteLine($"Hata Türü: {ex.GetType().Name}");
                _logger.LogError($"E-posta gönderilirken hata: {ex.Message}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"İç Hata: {ex.InnerException.Message}");
                    Console.WriteLine($"İç Hata Türü: {ex.InnerException.GetType().Name}");
                    _logger.LogError($"İç hata: {ex.InnerException.Message}");
                }
                return false;
            }
        }

        /// <summary>
        /// Uygulama içi bildirim ve e-posta gönderir
        /// </summary>
        public async Task SendNotificationAndEmail(int userId, int reservationId, string message, string emailSubject, string emailBody)
        {
            Console.WriteLine($"==== SendNotificationAndEmail metoduna giriş (UserID: {userId}, ReservationID: {reservationId}) ====");
            try
            {
                // Kullanıcıyı bul
                Console.WriteLine($"Kullanıcı verisi alınıyor (ID: {userId})");
                var user = await _context.Users.FindAsync(userId);
                if (user == null)
                {
                    _logger.LogError($"Bildirim gönderilirken hata: Kullanıcı (ID: {userId}) bulunamadı");
                    Console.WriteLine($"HATA: Kullanıcı bulunamadı (ID: {userId})");
                    return;
                }
                Console.WriteLine($"Kullanıcı bulundu: FullName={user.FullName}, Email={user.Email}");

                // Veritabanına bildirim kaydet
                Console.WriteLine("Bildirim veritabanına kaydediliyor...");
                var notification = new Notification
                {
                    UserId = userId,
                    ReservationId = reservationId,
                    Message = message,
                    SentAt = DateTime.UtcNow
                };

                _context.Notifications.Add(notification);
                await _context.SaveChangesAsync();
                Console.WriteLine($"Bildirim başarıyla veritabanına kaydedildi, ID: {notification.Id}");
                _logger.LogInformation($"Bildirim başarıyla kaydedildi. ID: {notification.Id}, Kullanıcı: {userId}");

                // E-posta gönder
                Console.WriteLine($"E-posta gönderiliyor: Alıcı={user.Email}, Konu={emailSubject}");
                bool emailResult = await SendEmail(user.Email, emailSubject, emailBody);
                Console.WriteLine($"E-posta gönderim sonucu: {(emailResult ? "BAŞARILI" : "BAŞARISIZ")}");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Bildirim ve e-posta gönderilirken hata oluştu: {ex.Message}");
                Console.WriteLine($"HATA: SendNotificationAndEmail metodunda hata: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"İç hata: {ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// Rezervasyon oluşturulduğunda bildirim gönderir
        /// </summary>
        public async Task SendReservationCreatedNotification(int reservationId)
        {
            try
            {
                // Rezervasyonu ve kullanıcı bilgilerini al
                var reservation = await _context.Reservations
                    .Include(r => r.User)
                    .Include(r => r.Resource)
                    .FirstOrDefaultAsync(r => r.Id == reservationId);

                if (reservation == null)
                {
                    _logger.LogError($"Rezervasyon bildirim hatası: Rezervasyon (ID: {reservationId}) bulunamadı");
                    return;
                }

                // Bildirim oluştur
                string notificationMessage = $"{reservation.Resource.Name} için {reservation.StartTime.ToLocalTime():dd.MM.yyyy HH:mm} - {reservation.EndTime.ToLocalTime():HH:mm} rezervasyonunuz oluşturuldu.";
                
                // E-posta içeriği oluştur
                string emailSubject = "Rezervasyon Onayı";
                string emailBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px;'>
                        <h2 style='color: #4CAF50;'>Rezervasyon Onayı</h2>
                        <p>Sayın {reservation.User.FullName},</p>
                        <p>Rezervasyonunuz başarıyla oluşturulmuştur.</p>
                        <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 15px 0;'>
                            <p><strong>Rezervasyon Detayları:</strong></p>
                            <p>Kaynak: {reservation.Resource.Name}</p>
                            <p>Tarih: {reservation.StartTime.ToLocalTime():dd.MM.yyyy}</p>
                            <p>Saat: {reservation.StartTime.ToLocalTime():HH:mm} - {reservation.EndTime.ToLocalTime():HH:mm}</p>
                            <p>Durum: {GetStatusText(reservation.Status)}</p>
                        </div>
                        <p>Rezervasyon durumunu görmek için <a href='https://eturezervasyon.erzurum.edu.tr'>sistem</a> üzerinden takip edebilirsiniz.</p>
                        <p style='font-size: 12px; color: #777; margin-top: 30px; border-top: 1px solid #eee; padding-top: 10px;'>
                            Bu e-posta ETÜ Rezervasyon Sistemi tarafından otomatik olarak gönderilmiştir. Lütfen bu e-postayı yanıtlamayınız.
                        </p>
                    </div>
                </body>
                </html>";

                // Bildirim ve e-posta gönder
                await SendNotificationAndEmail(reservation.UserId, reservationId, notificationMessage, emailSubject, emailBody);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Rezervasyon oluşturma bildirimi gönderilirken hata oluştu: {ex.Message}");
                Console.WriteLine($"Rezervasyon oluşturma bildirimi gönderilirken hata oluştu: {ex.Message}");
            }
        }

        /// <summary>
        /// Rezervasyon iptal edildiğinde bildirim gönderir
        /// </summary>
        public async Task SendReservationCancelledNotification(int reservationId, string cancelledBy)
        {
            try
            {
                // Rezervasyonu ve kullanıcı bilgilerini al
                var reservation = await _context.Reservations
                    .Include(r => r.User)
                    .Include(r => r.Resource)
                    .FirstOrDefaultAsync(r => r.Id == reservationId);

                if (reservation == null)
                {
                    _logger.LogError($"Rezervasyon iptal bildirimi hatası: Rezervasyon (ID: {reservationId}) bulunamadı");
                    return;
                }

                // Bildirim oluştur
                string notificationMessage = $"{reservation.Resource.Name} için {reservation.StartTime.ToLocalTime():dd.MM.yyyy HH:mm} - {reservation.EndTime.ToLocalTime():HH:mm} rezervasyonunuz iptal edildi.";
                if (!string.IsNullOrEmpty(cancelledBy) && cancelledBy != "user")
                {
                    notificationMessage = $"{reservation.Resource.Name} için {reservation.StartTime.ToLocalTime():dd.MM.yyyy HH:mm} - {reservation.EndTime.ToLocalTime():HH:mm} rezervasyonunuz {cancelledBy} tarafından iptal edildi.";
                }
                
                // E-posta içeriği oluştur
                string emailSubject = "Rezervasyon İptali";
                string cancelInfo = !string.IsNullOrEmpty(cancelledBy) && cancelledBy != "user" 
                    ? $"<p>{cancelledBy} tarafından iptal edilmiştir.</p>" 
                    : "<p>Talebiniz üzerine iptal edilmiştir.</p>";
                
                string emailBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px;'>
                        <h2 style='color: #F44336;'>Rezervasyon İptali</h2>
                        <p>Sayın {reservation.User.FullName},</p>
                        <p>Rezervasyonunuz iptal edilmiştir.</p>
                        {cancelInfo}
                        <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 15px 0;'>
                            <p><strong>Rezervasyon Detayları:</strong></p>
                            <p>Kaynak: {reservation.Resource.Name}</p>
                            <p>Tarih: {reservation.StartTime.ToLocalTime():dd.MM.yyyy}</p>
                            <p>Saat: {reservation.StartTime.ToLocalTime():HH:mm} - {reservation.EndTime.ToLocalTime():HH:mm}</p>
                        </div>
                        <p>Farklı bir zaman dilimi için yeni rezervasyon oluşturmak için <a href='https://eturezervasyon.erzurum.edu.tr'>sistem</a> üzerinden işlem yapabilirsiniz.</p>
                        <p style='font-size: 12px; color: #777; margin-top: 30px; border-top: 1px solid #eee; padding-top: 10px;'>
                            Bu e-posta ETÜ Rezervasyon Sistemi tarafından otomatik olarak gönderilmiştir. Lütfen bu e-postayı yanıtlamayınız.
                        </p>
                    </div>
                </body>
                </html>";

                // Bildirim ve e-posta gönder
                await SendNotificationAndEmail(reservation.UserId, reservationId, notificationMessage, emailSubject, emailBody);
            }
            catch (Exception ex)
            {
                _logger.LogError($"Rezervasyon iptal bildirimi gönderilirken hata oluştu: {ex.Message}");
                Console.WriteLine($"Rezervasyon iptal bildirimi gönderilirken hata oluştu: {ex.Message}");
            }
        }

        /// <summary>
        /// Rezervasyon onaylandığında bildirim gönderir
        /// </summary>
        public async Task SendReservationApprovedNotification(int reservationId, string approvedBy)
        {
            Console.WriteLine($"==== SendReservationApprovedNotification metoduna giriş yapıldı (ID: {reservationId}) ====");
            try
            {
                // Rezervasyonu ve kullanıcı bilgilerini al
                Console.WriteLine($"Rezervasyon verisi alınıyor (ID: {reservationId})");
                var reservation = await _context.Reservations
                    .Include(r => r.User)
                    .Include(r => r.Resource)
                    .FirstOrDefaultAsync(r => r.Id == reservationId);

                if (reservation == null)
                {
                    _logger.LogError($"Rezervasyon onay bildirimi hatası: Rezervasyon (ID: {reservationId}) bulunamadı");
                    Console.WriteLine($"HATA: Rezervasyon bulunamadı (ID: {reservationId})");
                    return;
                }
                Console.WriteLine($"Rezervasyon bulundu: UserID={reservation.UserId}, ResourceID={reservation.ResourceId}, Status={reservation.Status}");

                // Bildirim oluştur
                string notificationMessage = $"{reservation.Resource.Name} için {reservation.StartTime.ToLocalTime():dd.MM.yyyy HH:mm} - {reservation.EndTime.ToLocalTime():HH:mm} rezervasyonunuz onaylandı.";
                if (!string.IsNullOrEmpty(approvedBy))
                {
                    notificationMessage = $"{reservation.Resource.Name} için {reservation.StartTime.ToLocalTime():dd.MM.yyyy HH:mm} - {reservation.EndTime.ToLocalTime():HH:mm} rezervasyonunuz {approvedBy} tarafından onaylandı.";
                }
                Console.WriteLine($"Bildirim mesajı oluşturuldu: {notificationMessage}");
                
                // E-posta içeriği oluştur
                string emailSubject = "Rezervasyon Onaylandı";
                string approveInfo = !string.IsNullOrEmpty(approvedBy) 
                    ? $"<p>{approvedBy} tarafından onaylanmıştır.</p>" 
                    : "<p>Onaylanmıştır.</p>";
                
                string emailBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px;'>
                        <h2 style='color: #4CAF50;'>Rezervasyon Onaylandı</h2>
                        <p>Sayın {reservation.User.FullName},</p>
                        <p>Rezervasyon talebiniz onaylanmıştır.</p>
                        {approveInfo}
                        <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 15px 0;'>
                            <p><strong>Rezervasyon Detayları:</strong></p>
                            <p>Kaynak: {reservation.Resource.Name}</p>
                            <p>Tarih: {reservation.StartTime.ToLocalTime():dd.MM.yyyy}</p>
                            <p>Saat: {reservation.StartTime.ToLocalTime():HH:mm} - {reservation.EndTime.ToLocalTime():HH:mm}</p>
                        </div>
                        <p>Rezervasyon durumunu görmek için <a href='https://eturezervasyon.erzurum.edu.tr'>sistem</a> üzerinden takip edebilirsiniz.</p>
                        <p style='font-size: 12px; color: #777; margin-top: 30px; border-top: 1px solid #eee; padding-top: 10px;'>
                            Bu e-posta ETÜ Rezervasyon Sistemi tarafından otomatik olarak gönderilmiştir. Lütfen bu e-postayı yanıtlamayınız.
                        </p>
                    </div>
                </body>
                </html>";
                Console.WriteLine($"E-posta içeriği oluşturuldu, Konu: {emailSubject}");

                // Bildirim ve e-posta gönder
                Console.WriteLine($"SendNotificationAndEmail metodu çağrılıyor (UserID: {reservation.UserId}, ReservationID: {reservationId})");
                await SendNotificationAndEmail(reservation.UserId, reservationId, notificationMessage, emailSubject, emailBody);
                Console.WriteLine($"Bildirim ve e-posta gönderimi tamamlandı");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Rezervasyon onay bildirimi gönderilirken hata oluştu: {ex.Message}");
                Console.WriteLine($"HATA: Rezervasyon onay bildirimi gönderilirken hata: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"İç hata: {ex.InnerException.Message}");
                }
            }
        }

        /// <summary>
        /// Rezervasyon reddedildiğinde bildirim gönderir
        /// </summary>
        public async Task SendReservationRejectedNotification(int reservationId, string rejectedBy)
        {
            Console.WriteLine($"==== SendReservationRejectedNotification metoduna giriş yapıldı (ID: {reservationId}) ====");
            try
            {
                // Rezervasyonu ve kullanıcı bilgilerini al
                Console.WriteLine($"Rezervasyon verisi alınıyor (ID: {reservationId})");
                var reservation = await _context.Reservations
                    .Include(r => r.User)
                    .Include(r => r.Resource)
                    .FirstOrDefaultAsync(r => r.Id == reservationId);

                if (reservation == null)
                {
                    _logger.LogError($"Rezervasyon red bildirimi hatası: Rezervasyon (ID: {reservationId}) bulunamadı");
                    Console.WriteLine($"HATA: Rezervasyon bulunamadı (ID: {reservationId})");
                    return;
                }
                Console.WriteLine($"Rezervasyon bulundu: UserID={reservation.UserId}, ResourceID={reservation.ResourceId}, Status={reservation.Status}");

                // Bildirim oluştur
                string notificationMessage = $"{reservation.Resource.Name} için {reservation.StartTime.ToLocalTime():dd.MM.yyyy HH:mm} - {reservation.EndTime.ToLocalTime():HH:mm} rezervasyonunuz reddedildi.";
                if (!string.IsNullOrEmpty(rejectedBy))
                {
                    notificationMessage = $"{reservation.Resource.Name} için {reservation.StartTime.ToLocalTime():dd.MM.yyyy HH:mm} - {reservation.EndTime.ToLocalTime():HH:mm} rezervasyonunuz {rejectedBy} tarafından reddedildi.";
                }
                Console.WriteLine($"Bildirim mesajı oluşturuldu: {notificationMessage}");
                
                // E-posta içeriği oluştur
                string emailSubject = "Rezervasyon Reddedildi";
                string rejectInfo = !string.IsNullOrEmpty(rejectedBy) 
                    ? $"<p>{rejectedBy} tarafından reddedilmiştir.</p>" 
                    : "<p>Reddedilmiştir.</p>";
                
                string emailBody = $@"
                <html>
                <body style='font-family: Arial, sans-serif; color: #333;'>
                    <div style='max-width: 600px; margin: 0 auto; padding: 20px; border: 1px solid #ddd; border-radius: 5px;'>
                        <h2 style='color: #F44336;'>Rezervasyon Reddedildi</h2>
                        <p>Sayın {reservation.User.FullName},</p>
                        <p>Rezervasyon talebiniz reddedilmiştir.</p>
                        {rejectInfo}
                        <div style='background-color: #f9f9f9; padding: 15px; border-radius: 5px; margin: 15px 0;'>
                            <p><strong>Rezervasyon Detayları:</strong></p>
                            <p>Kaynak: {reservation.Resource.Name}</p>
                            <p>Tarih: {reservation.StartTime.ToLocalTime():dd.MM.yyyy}</p>
                            <p>Saat: {reservation.StartTime.ToLocalTime():HH:mm} - {reservation.EndTime.ToLocalTime():HH:mm}</p>
                        </div>
                        <p>Farklı bir zaman dilimi için yeni rezervasyon oluşturmak için <a href='https://eturezervasyon.erzurum.edu.tr'>sistem</a> üzerinden işlem yapabilirsiniz.</p>
                        <p style='font-size: 12px; color: #777; margin-top: 30px; border-top: 1px solid #eee; padding-top: 10px;'>
                            Bu e-posta ETÜ Rezervasyon Sistemi tarafından otomatik olarak gönderilmiştir. Lütfen bu e-postayı yanıtlamayınız.
                        </p>
                    </div>
                </body>
                </html>";
                Console.WriteLine($"E-posta içeriği oluşturuldu, Konu: {emailSubject}");

                // Bildirim ve e-posta gönder
                Console.WriteLine($"SendNotificationAndEmail metodu çağrılıyor (UserID: {reservation.UserId}, ReservationID: {reservationId})");
                await SendNotificationAndEmail(reservation.UserId, reservationId, notificationMessage, emailSubject, emailBody);
                Console.WriteLine($"Bildirim ve e-posta gönderimi tamamlandı");
            }
            catch (Exception ex)
            {
                _logger.LogError($"Rezervasyon red bildirimi gönderilirken hata oluştu: {ex.Message}");
                Console.WriteLine($"HATA: Rezervasyon red bildirimi gönderilirken hata: {ex.Message}");
                Console.WriteLine($"Stack Trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"İç hata: {ex.InnerException.Message}");
                }
            }
        }

        // Rezervasyon durumunun insanca okunabilir halini döndürür
        private string GetStatusText(string status)
        {
            return status.ToLower() switch
            {
                "pending" => "Beklemede",
                "approved" => "Onaylandı",
                "rejected" => "Reddedildi",
                "cancelled" => "İptal Edildi",
                _ => status
            };
        }
    }
} 