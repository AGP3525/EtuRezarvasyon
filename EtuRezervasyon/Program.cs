using Microsoft.EntityFrameworkCore;
using Microsoft.AspNetCore.Authentication.Cookies;
using Microsoft.AspNetCore.Authentication.JwtBearer;
using Microsoft.IdentityModel.Tokens;
using System.Text;
using EtuRezervasyon.Services;
using Microsoft.OpenApi.Models;

var builder = WebApplication.CreateBuilder(args);

// Add services to the container.
builder.Services.AddControllersWithViews();

// PostgreSQL ve Entity Framework yapılandırması
builder.Services.AddDbContext<EtuRezervasyon.Data.AppDbContext>(options =>
    options.UseNpgsql(builder.Configuration.GetConnectionString("DefaultConnection")));

// Session ve Authentication servisleri
builder.Services.AddSession(options =>
{
    options.IdleTimeout = TimeSpan.FromMinutes(30);
    options.Cookie.HttpOnly = true;
    options.Cookie.IsEssential = true;
});

// Kimlik doğrulama servislerini düzgün şekilde yapılandırma
builder.Services.AddAuthentication(options => 
{
    // Web sayfaları için cookie, API çağrıları için JWT
    options.DefaultScheme = "JWT_OR_COOKIE";
    options.DefaultSignInScheme = CookieAuthenticationDefaults.AuthenticationScheme;
    options.DefaultChallengeScheme = "JWT_OR_COOKIE";
})
// Cookie kimlik doğrulama
.AddCookie(CookieAuthenticationDefaults.AuthenticationScheme, options =>
{
    options.LoginPath = "/SignIn";
    options.AccessDeniedPath = "/Home/AccessDenied";
    options.Cookie.Name = "EtuRezervasyonAuth";
    options.Cookie.HttpOnly = true;
    options.ExpireTimeSpan = TimeSpan.FromDays(7);
    options.SlidingExpiration = true;
})
// JWT kimlik doğrulama
.AddJwtBearer(JwtBearerDefaults.AuthenticationScheme, options =>
{
    options.TokenValidationParameters = new TokenValidationParameters
    {
        ValidateIssuer = true,
        ValidateAudience = true,
        ValidateLifetime = true,
        ValidateIssuerSigningKey = true,
        ValidIssuer = builder.Configuration["Jwt:Issuer"],
        ValidAudience = builder.Configuration["Jwt:Audience"],
        IssuerSigningKey = new SymmetricSecurityKey(
            Encoding.UTF8.GetBytes(builder.Configuration["Jwt:Key"] ?? throw new InvalidOperationException("JWT key is not configured."))
        ),
        ClockSkew = TimeSpan.Zero
    };
    
    // JWT hata ayıklama için
    options.Events = new JwtBearerEvents
    {
        OnAuthenticationFailed = context =>
        {
            Console.WriteLine($"Authentication failed: {context.Exception.Message}");
            return Task.CompletedTask;
        },
        OnTokenValidated = context =>
        {
            Console.WriteLine("Token validated successfully");
            return Task.CompletedTask;
        }
    };
})
// Politika şeması - URL veya endpoint'e göre uygun kimlik doğrulama şemasını seçmek için
.AddPolicyScheme("JWT_OR_COOKIE", "JWT or Cookie", options =>
{
    options.ForwardDefaultSelector = context =>
    {
        // API istekleri için JWT kullan
        string authorization = context.Request.Headers.Authorization.FirstOrDefault() ?? string.Empty;
        if (authorization.StartsWith("Bearer "))
            return JwtBearerDefaults.AuthenticationScheme;
            
        // API url'leri için JWT kullan
        if (context.Request.Path.StartsWithSegments("/api"))
            return JwtBearerDefaults.AuthenticationScheme;
            
        // Diğer tüm istekler için cookie kimlik doğrulama kullan
        return CookieAuthenticationDefaults.AuthenticationScheme;
    };
});

// JWT services
builder.Services.AddScoped<TokenService>();
builder.Services.AddScoped<AuthService>();
builder.Services.AddScoped<NotificationService>();

// Swagger yapılandırması
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen(c =>
{
    c.SwaggerDoc("v1", new OpenApiInfo { 
        Title = "ETU Rezervasyon API", 
        Version = "v1",
        Description = "TOBB ETÜ Rezervasyon Sistemi API'si"
    });
    
    // Swagger'da JWT kimlik doğrulama için
    c.AddSecurityDefinition("Bearer", new OpenApiSecurityScheme
    {
        Description = "JWT Authorization header using the Bearer scheme. Example: \"Authorization: Bearer {token}\"",
        Name = "Authorization",
        In = ParameterLocation.Header,
        Type = SecuritySchemeType.ApiKey,
        Scheme = "Bearer"
    });

    c.AddSecurityRequirement(new OpenApiSecurityRequirement
    {
        {
            new OpenApiSecurityScheme
            {
                Reference = new OpenApiReference
                {
                    Type = ReferenceType.SecurityScheme,
                    Id = "Bearer"
                }
            },
            Array.Empty<string>()
        }
    });
});

var app = builder.Build();

// Configure the HTTP request pipeline.
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI(c => {
        c.SwaggerEndpoint("/swagger/v1/swagger.json", "ETU Rezervasyon API v1");
    });
}
else
{
    app.UseExceptionHandler("/Home/Error");
    app.UseHsts();
}

app.UseHttpsRedirection();
app.UseStaticFiles();

app.UseRouting();

// Session middleware
app.UseSession();

// Authentication ve Authorization middleware - sıralama önemli!
app.UseAuthentication();
app.UseAuthorization();

app.MapControllerRoute(
    name: "default",
    pattern: "{controller=Home}/{action=Index}/{id?}");

// Veritabanını oluştur ve gerekli verileri kontrol et
using (var scope = app.Services.CreateScope())
{
    var services = scope.ServiceProvider;
    try
    {
        var context = services.GetRequiredService<EtuRezervasyon.Data.AppDbContext>();
        context.Database.Migrate();
        
        // Roller yoksa ekle
        if (!context.Roles.Any())
        {
            context.Roles.AddRange(
                new EtuRezervasyon.Models.Role { Id = 1, Name = "Admin" },
                new EtuRezervasyon.Models.Role { Id = 2, Name = "User" }
            );
            context.SaveChanges();
            Console.WriteLine("Roller başarıyla eklendi.");
        }
        
        // Kullanıcı yoksa ekle
        if (!context.Users.Any())
        {
            context.Users.Add(new EtuRezervasyon.Models.User
            {
                FullName = "Admin User",
                Email = "admin@erzurum.edu.tr",
                PasswordHash = "admin123", // Gerçek uygulamada hash kullanılmalı
                RoleId = 1, // Admin rolü
                CreatedAt = DateTime.Now
            });
            context.SaveChanges();
            Console.WriteLine("Admin kullanıcısı başarıyla eklendi.");
        }
        
        // Kaynak yoksa varsayılan kaynakları ekle
        if (!context.Resources.Any())
        {
            Console.WriteLine("Veritabanında hiç kaynak bulunamadı. Varsayılan kaynaklar ekleniyor...");
            context.Resources.AddRange(
                new EtuRezervasyon.Models.Resource
                {
                    Name = "Kütüphane",
                    Type = "library",
                    Capacity = 100,
                    Location = "Ana Bina",
                    CreatedAt = DateTime.Now
                },
                new EtuRezervasyon.Models.Resource
                {
                    Name = "Proje Odası",
                    Type = "room",
                    Capacity = 10,
                    Location = "C Blok",
                    CreatedAt = DateTime.Now
                },
                new EtuRezervasyon.Models.Resource
                {
                    Name = "Konferans Salonu",
                    Type = "conference",
                    Capacity = 200,
                    Location = "B Blok",
                    CreatedAt = DateTime.Now
                }
            );
            context.SaveChanges();
            Console.WriteLine("Varsayılan kaynaklar başarıyla eklendi.");
        }
        
        // Varolan Resource kayıtlarını kontrol et - tip düzeltmeleri
        var resources = context.Resources.ToList();
        bool hasChanges = false;
        
        // Kütüphane kaynağını kontrol et ve düzelt
        var library = resources.FirstOrDefault(r => 
            r.Name.Contains("Kütüphane", StringComparison.OrdinalIgnoreCase) || 
            r.Type.Contains("library", StringComparison.OrdinalIgnoreCase) ||
            r.Type.Contains("kütüphane", StringComparison.OrdinalIgnoreCase));
        
        if (library != null && library.Type != "library")
        {
            library.Type = "library";
            hasChanges = true;
            Console.WriteLine($"Kütüphane kaynağının tipi 'library' olarak düzeltildi. ID: {library.Id}");
        }
        
        // Proje Odası kaynağını kontrol et ve düzelt
        var room = resources.FirstOrDefault(r => 
            r.Name.Contains("Proje", StringComparison.OrdinalIgnoreCase) || 
            r.Name.Contains("Oda", StringComparison.OrdinalIgnoreCase) ||
            r.Type.Contains("room", StringComparison.OrdinalIgnoreCase) ||
            r.Type.Contains("oda", StringComparison.OrdinalIgnoreCase) ||
            r.Type.Contains("çalışma", StringComparison.OrdinalIgnoreCase));
            
        if (room != null && room.Type != "room")
        {
            room.Type = "room";
            hasChanges = true;
            Console.WriteLine($"Proje Odası kaynağının tipi 'room' olarak düzeltildi. ID: {room.Id}");
        }
        
        // Konferans Salonu kaynağını kontrol et ve düzelt
        var conference = resources.FirstOrDefault(r => 
            r.Name.Contains("Konferans", StringComparison.OrdinalIgnoreCase) || 
            r.Name.Contains("Salon", StringComparison.OrdinalIgnoreCase) ||
            r.Type.Contains("conference", StringComparison.OrdinalIgnoreCase) ||
            r.Type.Contains("konferans", StringComparison.OrdinalIgnoreCase));
            
        if (conference != null && conference.Type != "conference")
        {
            conference.Type = "conference";
            hasChanges = true;
            Console.WriteLine($"Konferans Salonu kaynağının tipi 'conference' olarak düzeltildi. ID: {conference.Id}");
        }
        
        // Değişiklikler varsa kaydet
        if (hasChanges)
        {
            context.SaveChanges();
            Console.WriteLine("Kaynak tiplerinde düzeltmeler yapıldı.");
        }
        
        // Mevcut kaynakların durumunu logla
        Console.WriteLine("\n=== MEVCUT KAYNAKLAR ===");
        foreach (var resource in context.Resources.ToList())
        {
            Console.WriteLine($"ID: {resource.Id}, İsim: {resource.Name}, Tip: {resource.Type}, Lokasyon: {resource.Location}");
        }
        Console.WriteLine("=======================\n");
    }
    catch (Exception ex)
    {
        var logger = services.GetRequiredService<ILogger<Program>>();
        logger.LogError(ex, "Veritabanı başlatılırken bir hata oluştu.");
    }
}

app.Run();