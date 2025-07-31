using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using EtuRezervasyon.Data;
using System.Security.Claims;
using Microsoft.AspNetCore.Authentication;
using Microsoft.AspNetCore.Authentication.Cookies;
using System.Security.Cryptography;
using System.Text;
using System.Net.Mail;
using System.Net;
using Microsoft.Extensions.DependencyInjection;
using EtuRezervasyon.Services;
using EtuRezervasyon.Models;

namespace EtuRezervasyon.Controllers
{
    public class SigninController : Controller
    {
        private readonly AppDbContext _context;
        private readonly Random _random = new Random();
        private readonly EtuRezervasyon.Services.AuthService _authService;
        private readonly IConfiguration _configuration;
        private readonly IServiceProvider app;

        public SigninController(AppDbContext context, EtuRezervasyon.Services.AuthService authService, IConfiguration configuration, IServiceProvider app)
        {
            _context = context;
            _authService = authService;
            _configuration = configuration;
            this.app = app;
        }

        [HttpGet]
        public IActionResult Index()
        {
            // Kullanıcı zaten giriş yapmışsa ana sayfaya yönlendir
            if (User.Identity != null && User.Identity.IsAuthenticated)
            {
                return RedirectToAction("Index", "Home");
            }

            return View();
        }

        [HttpPost]
        public async Task<IActionResult> Login(string email, string password, bool rememberMe)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(password))
            {
                TempData["ErrorMessage"] = "Email ve şifre alanları boş olamaz.";
                return RedirectToAction("Index");
            }

            // Email ile kullanıcıyı bul
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => u.Email == email);

            // Kullanıcı bulunamadı
            if (user == null)
            {
                TempData["ErrorMessage"] = "Bu email adresine sahip bir kullanıcı bulunamadı.";
                return RedirectToAction("Index");
            }

            // Şifre kontrolü - Doğrudan veritabanındaki şifreyle karşılaştırma yapılıyor
            if (!PasswordHasher.VerifyPassword(password, user.PasswordHash))
            {
                TempData["ErrorMessage"] = "Şifre yanlış.";
                return RedirectToAction("Index");
            }

            // Admin kullanıcısı için iki faktörlü doğrulamayı atla
            if (email.ToLower() == "admin@erzurum.edu.tr")
            {
                // Admin için direkt giriş yap
                return await CompleteLogin(user, rememberMe);
            }

            // Doğrulama kodu oluştur ve gönder
            string twoFactorCode = GenerateTwoFactorCode();
            DateTime expiryTime = DateTime.UtcNow.AddMinutes(5); // 5 dakika geçerli
            
            // Kodu kullanıcı veritabanına kaydet
            user.TwoFactorCode = twoFactorCode;
            user.TwoFactorCodeExpiry = expiryTime;
            await _context.SaveChangesAsync();
            
            // Doğrulama kodunu e-posta ile gönder
            await SendTwoFactorCodeEmail(user.Email, twoFactorCode);
            
            // Doğrulama sayfasına yönlendir ve email adresini geçir
            TempData["VerificationEmail"] = email;
            TempData["RememberMe"] = rememberMe;
            TempData["InfoMessage"] = "Lütfen e-posta adresinize gönderilen doğrulama kodunu giriniz.";
            
            return RedirectToAction("VerifyTwoFactor");
        }
        
        // Giriş işlemini tamamlayan yardımcı metot
        private async Task<IActionResult> CompleteLogin(User user, bool rememberMe)
        {
            // Giriş başarılı, kullanıcı kimliği oluştur
            var claims = new List<Claim>
            {
                new Claim(ClaimTypes.NameIdentifier, user.UserId.ToString()),
                new Claim(ClaimTypes.Name, user.FullName),
                new Claim(ClaimTypes.Email, user.Email)
            };

            // Kullanıcı rolü varsa ekle
            if (user.Role != null)
            {
                claims.Add(new Claim(ClaimTypes.Role, user.Role.Name));
            }

            var claimsIdentity = new ClaimsIdentity(claims, CookieAuthenticationDefaults.AuthenticationScheme);
            var authProperties = new AuthenticationProperties
            {
                IsPersistent = rememberMe, // "Beni hatırla" seçeneği
                ExpiresUtc = DateTimeOffset.UtcNow.AddDays(7) // 7 gün
            };

            await HttpContext.SignInAsync(
                CookieAuthenticationDefaults.AuthenticationScheme,
                new ClaimsPrincipal(claimsIdentity),
                authProperties);

            // Giriş başarılı, rolüne göre yönlendir
            if (user.Role != null && user.Role.Id == 1)
            {
                return RedirectToAction("Index", "SuperAdmin");
            }
            
            return RedirectToAction("Index", "Home");
        }
        
        [HttpGet]
        public IActionResult VerifyTwoFactor()
        {
            // Eğer email bilgisi yoksa login sayfasına yönlendir
            if (TempData["VerificationEmail"] == null)
            {
                return RedirectToAction("Index");
            }
            
            // TempData'dan verileri al ve ViewBag'e aktar
            ViewBag.Email = TempData["VerificationEmail"]?.ToString();
            ViewBag.RememberMe = TempData["RememberMe"];
            TempData.Keep("VerificationEmail"); // Bir sonraki istek için tut
            TempData.Keep("RememberMe"); // Bir sonraki istek için tut
            
            return View();
        }
        
        [HttpPost]
        public async Task<IActionResult> VerifyTwoFactor(string email, string verificationCode, bool rememberMe)
        {
            if (string.IsNullOrEmpty(email) || string.IsNullOrEmpty(verificationCode))
            {
                TempData["ErrorMessage"] = "Email ve doğrulama kodu alanları boş olamaz.";
                return RedirectToAction("VerifyTwoFactor");
            }
            
            // Email ile kullanıcıyı bul
            var user = await _context.Users
                .Include(u => u.Role)
                .FirstOrDefaultAsync(u => 
                    u.Email == email && 
                    u.TwoFactorCode == verificationCode && 
                    u.TwoFactorCodeExpiry > DateTime.UtcNow);
            
            if (user == null)
            {
                TempData["ErrorMessage"] = "Geçersiz veya süresi dolmuş doğrulama kodu.";
                return RedirectToAction("VerifyTwoFactor");
            }
            
            // Doğrulama kodunu sıfırla
            user.TwoFactorCode = null;
            user.TwoFactorCodeExpiry = null;
            await _context.SaveChangesAsync();
            
            // Giriş işlemini tamamla
            return await CompleteLogin(user, rememberMe);
        }
        
        // İki faktörlü doğrulama kodu oluştur
        private string GenerateTwoFactorCode()
        {
            return _random.Next(100000, 1000000).ToString("D6");
        }
        
        // İki faktörlü doğrulama kodu gönder
        private async Task SendTwoFactorCodeEmail(string email, string code)
        {
            try
            {
                // E-posta ayarlarını appsettings.json'dan al
                string smtpServer = _configuration["Email:SmtpServer"] ?? "smtp.gmail.com";
                int smtpPort = int.TryParse(_configuration["Email:SmtpPort"], out int port) ? port : 587;
                string smtpUsername = _configuration["Email:Username"] ?? _configuration["Email:FromEmail"] ?? "";
                string smtpPassword = _configuration["Email:Password"] ?? "";
                string fromEmail = _configuration["Email:FromEmail"] ?? smtpUsername;
                string fromName = _configuration["Email:FromName"] ?? "ETU Rezervasyon";

                // Konsola mevcut ayarları yazdır
                Console.WriteLine("SMTP Ayarları (İki Faktörlü Doğrulama Kodu):");
                Console.WriteLine($"Server: {smtpServer}");
                Console.WriteLine($"Port: {smtpPort}");
                Console.WriteLine($"Username: {smtpUsername}");
                Console.WriteLine($"Password: {(string.IsNullOrEmpty(smtpPassword) ? "BOŞ!" : "****")}");
                Console.WriteLine($"From: {fromEmail}");
                Console.WriteLine($"To: {email}");

                using var client = new SmtpClient(smtpServer, smtpPort)
                {
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = true,
                    Timeout = 30000 // 30 saniye
                };

                // E-posta içeriği oluştur
                string body = $@"
                <html>
                    <body style='font-family: Arial, sans-serif; color: #333;'>
                        <h2>ETU Rezervasyon Giriş Doğrulama Kodu</h2>
                        <p>Sisteme giriş yapmak için aşağıdaki 6 haneli kodu kullanın:</p>
                        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 5px;'>
                            {code}
                        </div>
                        <p>Bu kod 5 dakika boyunca geçerlidir.</p>
                        <p>Eğer giriş yapmayı denemediyseniz, bu e-postayı görmezden gelebilirsiniz.</p>
                        <p>Saygılarımızla,<br/>ETU Rezervasyon Ekibi</p>
                    </body>
                </html>";

                using var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = "ETU Rezervasyon - Giriş Doğrulama Kodu",
                    Body = body,
                    IsBodyHtml = true
                };

                message.To.Add(email);
                Console.WriteLine("Mail hazırlandı, gönderiliyor...");

                await client.SendMailAsync(message);
                Console.WriteLine("E-posta başarıyla gönderildi!");
            }
            catch (Exception ex)
            {
                // Hata loglaması
                Console.WriteLine($"E-posta gönderilirken hata oluştu: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"İç hata: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        [HttpPost]
        public async Task<IActionResult> Logout()
        {
            await HttpContext.SignOutAsync(CookieAuthenticationDefaults.AuthenticationScheme);
            return RedirectToAction("Index", "Home");
        }

        public IActionResult Takvim()
        {
            return View();
        }

        [HttpPost]
        public async Task<IActionResult> ForgotPassword(string email)
        {
            if (string.IsNullOrEmpty(email))
            {
                TempData["ErrorMessage"] = "E-posta adresi boş olamaz.";
                return RedirectToAction("Index");
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == email);
            if (user == null)
            {
                // Veritabanında kayıtlı olmayan e-posta için hata mesajı
                TempData["ErrorMessage"] = "Bu e-posta adresi sistemde kayıtlı değil.";
                return RedirectToAction("Index");
            }

            // 4 haneli kod oluştur
            string resetCode = GenerateResetCode();
            
            // Kodun son kullanma tarihi (3 dakika)
            DateTime expireDate = DateTime.UtcNow.AddMinutes(3);
            
            // Kodu veritabanına kaydet
            user.ResetPasswordToken = resetCode;
            user.ResetPasswordTokenExpiry = expireDate;
            await _context.SaveChangesAsync();

            // E-posta gönder
            await SendResetCodeEmail(user.Email, resetCode);

            TempData["SuccessMessage"] = "Şifre sıfırlama kodu e-posta adresinize gönderildi.";
            return RedirectToAction("Index");
        }

        [HttpPost]
        public async Task<IActionResult> SendResetCode([FromBody] ResetCodeRequest request)
        {
            if (string.IsNullOrEmpty(request.Email))
            {
                return Json(new { success = false, message = "E-posta adresi boş olamaz." });
            }

            // Session'da aktif bir kod olup olmadığını kontrol et
            var resetCodeExpiry = HttpContext.Session.GetString("ResetCodeExpiry");
            if (!string.IsNullOrEmpty(resetCodeExpiry))
            {
                var expiryTime = DateTime.Parse(resetCodeExpiry);
                if (expiryTime > DateTime.UtcNow)
                {
                    // Aktif bir kod var ve süresi henüz dolmamış
                    return Json(new { success = true, hasActiveCode = true });
                }
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => u.Email == request.Email);
            if (user == null)
            {
                // Veritabanında kayıtlı olmayan e-posta adresleri için hata mesajı
                return Json(new { success = false, message = "Bu e-posta adresi sistemde kayıtlı değil." });
            }

            // 4 haneli kod oluştur
            string resetCode = GenerateResetCode();
            
            // Kodun son kullanma tarihi (3 dakika)
            DateTime expireDate = DateTime.UtcNow.AddMinutes(3);
            
            // Kodu veritabanına kaydet
            user.ResetPasswordToken = resetCode;
            user.ResetPasswordTokenExpiry = expireDate;
            await _context.SaveChangesAsync();

            // Session'a kodun son kullanma tarihini kaydet
            HttpContext.Session.SetString("ResetCodeExpiry", expireDate.ToString("o"));
            HttpContext.Session.SetString("ResetCodeEmail", user.Email);

            // E-posta gönder
            try
            {
                await SendResetCodeEmail(user.Email, resetCode);
                return Json(new { success = true, hasActiveCode = false });
            }
            catch(Exception ex)
            {
                // Hata loglaması yapılabilir
                Console.WriteLine($"E-posta gönderilirken hata oluştu: {ex.Message}");
                HttpContext.Session.Remove("ResetCodeExpiry");
                HttpContext.Session.Remove("ResetCodeEmail");
                return Json(new { success = false, message = "E-posta gönderilirken bir hata oluştu." });
            }
        }

        [HttpPost]
        public async Task<IActionResult> VerifyResetCode([FromBody] VerifyCodeRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Code))
            {
                return Json(new { success = false, message = "E-posta veya doğrulama kodu boş olamaz." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => 
                u.Email == request.Email && 
                u.ResetPasswordToken == request.Code &&
                u.ResetPasswordTokenExpiry > DateTime.UtcNow);

            if (user == null)
            {
                return Json(new { success = false, message = "Geçersiz veya süresi dolmuş doğrulama kodu." });
            }

            return Json(new { success = true });
        }

        [HttpPost]
        public async Task<IActionResult> ResetPasswordWithCode([FromBody] ResetPasswordWithCodeRequest request)
        {
            if (string.IsNullOrEmpty(request.Email) || string.IsNullOrEmpty(request.Code) || 
                string.IsNullOrEmpty(request.NewPassword) || string.IsNullOrEmpty(request.ConfirmNewPassword))
            {
                return Json(new { success = false, message = "Tüm alanlar doldurulmalıdır." });
            }

            if (request.NewPassword != request.ConfirmNewPassword)
            {
                return Json(new { success = false, message = "Şifreler eşleşmiyor." });
            }

            var user = await _context.Users.FirstOrDefaultAsync(u => 
                u.Email == request.Email && 
                u.ResetPasswordToken == request.Code &&
                u.ResetPasswordTokenExpiry > DateTime.UtcNow);

            if (user == null)
            {
                return Json(new { success = false, message = "Geçersiz veya süresi dolmuş doğrulama kodu." });
            }

            // Eski şifreyle aynı olup olmadığını kontrol et
            if (PasswordHasher.VerifyPassword(request.NewPassword, user.PasswordHash))
            {
                return Json(new { success = false, message = "Yeni şifreniz eski şifrenizle aynı olamaz. Lütfen farklı bir şifre deneyin." });
            }

            // Yeni şifreyi kaydet
            user.PasswordHash = PasswordHasher.HashPassword(request.NewPassword);

            // Token bilgilerini temizle
            user.ResetPasswordToken = null;
            user.ResetPasswordTokenExpiry = null;
            
            // Session'daki şifre sıfırlama bilgilerini temizle
            HttpContext.Session.Remove("ResetCodeExpiry");
            HttpContext.Session.Remove("ResetCodeEmail");
            
            await _context.SaveChangesAsync();

            // Şifre değişikliği bildirimi gönder
            await SendPasswordChangedEmail(request.Email);

            return Json(new { success = true, message = "Şifreniz başarıyla değiştirildi. Yeni şifrenizle giriş yapabilirsiniz." });
        }

        private string GenerateResetCode()
        {
            return _random.Next(1000, 10000).ToString("D4");
        }

        private async Task SendResetCodeEmail(string email, string resetCode)
        {
            try
            {
                // E-posta ayarlarını appsettings.json'dan al
                string smtpServer = _configuration["Email:SmtpServer"] ?? "smtp.gmail.com";
                int smtpPort = int.TryParse(_configuration["Email:SmtpPort"], out int port) ? port : 587;
                string smtpUsername = _configuration["Email:Username"] ?? _configuration["Email:FromEmail"] ?? "";
                string smtpPassword = _configuration["Email:Password"] ?? "";
                string fromEmail = _configuration["Email:FromEmail"] ?? smtpUsername;
                string fromName = _configuration["Email:FromName"] ?? "ETU Rezervasyon";

                // Konsola mevcut ayarları yazdır
                Console.WriteLine("SMTP Ayarları (Şifre Sıfırlama Kodu):");
                Console.WriteLine($"Server: {smtpServer}");
                Console.WriteLine($"Port: {smtpPort}");
                Console.WriteLine($"Username: {smtpUsername}");
                Console.WriteLine($"Password: {(string.IsNullOrEmpty(smtpPassword) ? "BOŞ!" : "****")}");
                Console.WriteLine($"From: {fromEmail}");
                Console.WriteLine($"To: {email}");

                using var client = new SmtpClient(smtpServer, smtpPort)
                {
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = true,
                    Timeout = 30000 // 30 saniye
                };

                // E-posta içeriği oluştur
                string body = $@"
                <html>
                    <body style='font-family: Arial, sans-serif; color: #333;'>
                        <h2>Şifre Sıfırlama Kodu</h2>
                        <p>Şifrenizi sıfırlamak için aşağıdaki 4 haneli kodu kullanın:</p>
                        <div style='background-color: #f8f9fa; padding: 15px; border-radius: 5px; text-align: center; font-size: 24px; font-weight: bold; letter-spacing: 5px;'>
                            {resetCode}
                        </div>
                        <p>Bu kod 3 dakika boyunca geçerlidir.</p>
                        <p>Eğer şifre sıfırlama talebinde bulunmadıysanız, bu e-postayı görmezden gelebilirsiniz.</p>
                        <p>Saygılarımızla,<br/>ETU Rezervasyon Ekibi</p>
                    </body>
                </html>";

                using var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = "ETU Rezervasyon - Şifre Sıfırlama Kodu",
                    Body = body,
                    IsBodyHtml = true
                };

                message.To.Add(email);
                Console.WriteLine("Mail hazırlandı, gönderiliyor...");

                await client.SendMailAsync(message);
                Console.WriteLine("E-posta başarıyla gönderildi!");
            }
            catch (Exception ex)
            {
                // Hata loglaması
                Console.WriteLine($"E-posta gönderilirken hata oluştu: {ex.Message}");
                Console.WriteLine($"StackTrace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"İç hata: {ex.InnerException.Message}");
                }
                throw;
            }
        }

        private async Task SendPasswordChangedEmail(string email)
        {
            try
            {
                // E-posta ayarlarını appsettings.json'dan al
                string smtpServer = _configuration["Email:SmtpServer"] ?? "smtp.gmail.com";
                int smtpPort = int.TryParse(_configuration["Email:SmtpPort"], out int port) ? port : 587;
                string smtpUsername = _configuration["Email:Username"] ?? _configuration["Email:FromEmail"] ?? "";
                string smtpPassword = _configuration["Email:Password"] ?? "";
                string fromEmail = _configuration["Email:FromEmail"] ?? smtpUsername;
                string fromName = _configuration["Email:FromName"] ?? "ETU Rezervasyon";

                // Konsola mevcut ayarları yazdır
                Console.WriteLine("SMTP Ayarları (Şifre Değiştirme Bildirimi):");
                Console.WriteLine($"Server: {smtpServer}");
                Console.WriteLine($"Port: {smtpPort}");
                Console.WriteLine($"Username: {smtpUsername}");
                Console.WriteLine($"From: {fromEmail}");
                Console.WriteLine($"To: {email}");

                using var client = new SmtpClient(smtpServer, smtpPort)
                {
                    UseDefaultCredentials = false,
                    Credentials = new NetworkCredential(smtpUsername, smtpPassword),
                    EnableSsl = true,
                    Timeout = 30000 // 30 saniye
                };

                // E-posta içeriği oluştur
                string body = $@"
                <html>
                    <body style='font-family: Arial, sans-serif; color: #333;'>
                        <h2>Şifre Değişikliği Bilgilendirmesi</h2>
                        <p>Sayın Kullanıcımız,</p>
                        <p>Şifreniz başarıyla değiştirilmiştir. Yeni şifrenizle hesabınıza giriş yapabilirsiniz.</p>
                        <p>Eğer bu değişikliği siz yapmadıysanız, lütfen en kısa sürede bizimle iletişime geçin.</p>
                        <p>Saygılarımızla,<br/>ETU Rezervasyon Ekibi</p>
                    </body>
                </html>";

                using var message = new MailMessage
                {
                    From = new MailAddress(fromEmail, fromName),
                    Subject = "ETU Rezervasyon - Şifre Değişikliği Bilgilendirmesi",
                    Body = body,
                    IsBodyHtml = true
                };

                message.To.Add(email);
                Console.WriteLine("Mail hazırlandı, gönderiliyor...");

                await client.SendMailAsync(message);
                Console.WriteLine("E-posta başarıyla gönderildi!");
            }
            catch (Exception ex)
            {
                // Hata loglaması, ancak hata fırlatmadan devam et
                Console.WriteLine($"Şifre değişikliği bilgilendirme e-postası gönderilirken hata oluştu: {ex.Message}");
                Console.WriteLine($"Stack trace: {ex.StackTrace}");
                if (ex.InnerException != null)
                {
                    Console.WriteLine($"Inner exception: {ex.InnerException.Message}");
                }
            }
        }

        // Model sınıfları
        public class ResetCodeRequest
        {
            public required string Email { get; set; }
        }

        public class VerifyCodeRequest
        {
            public required string Email { get; set; }
            public required string Code { get; set; }
        }

        public class ResetPasswordWithCodeRequest
        {
            public required string Email { get; set; }
            public required string Code { get; set; }
            public required string NewPassword { get; set; }
            public required string ConfirmNewPassword { get; set; }
        }
    }
}



