using Microsoft.EntityFrameworkCore;
using EtuRezervasyon.Models;
using EtuRezervasyon.Services;

namespace EtuRezervasyon.Data
{
    public class AppDbContext : DbContext
    {
        public AppDbContext(DbContextOptions<AppDbContext> options) : base(options) { }
        public DbSet<Reservation> Reservations { get; set; }
        public DbSet<Role> Roles { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Resource> Resources { get; set; }
        public DbSet<Notification> Notifications { get; set; }

protected override void OnModelCreating(ModelBuilder modelBuilder)
{
    base.OnModelCreating(modelBuilder);

    // Tablolar arasındaki ilişkiler
    modelBuilder.Entity<User>()
        .HasOne(u => u.Role)
        .WithMany(r => r.Users)
        .HasForeignKey(u => u.RoleId)
        .OnDelete(DeleteBehavior.SetNull);

    modelBuilder.Entity<Reservation>()
        .HasIndex(r => new { r.ResourceId, r.StartTime, r.EndTime })
        .IsUnique();

    modelBuilder.Entity<Reservation>()
        .HasOne(r => r.User)
        .WithMany(u => u.Reservations)
        .HasForeignKey(r => r.UserId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<Reservation>()
        .HasOne(r => r.Resource)
        .WithMany(res => res.Reservations)
        .HasForeignKey(r => r.ResourceId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<Notification>()
        .HasOne(n => n.User)
        .WithMany(u => u.Notifications)
        .HasForeignKey(n => n.UserId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<Notification>()
        .HasOne(n => n.Reservation)
        .WithMany(r => r.Notifications)
        .HasForeignKey(n => n.ReservationId)
        .OnDelete(DeleteBehavior.Cascade);

    modelBuilder.Entity<Role>().HasData(
        new Role
        {
            Id = 1,
            Name = "Admin"
        }
    );

    modelBuilder.Entity<Role>().HasData(
        new Role
        {
            Id = 2,
            Name = "Akademisyen"
        }
    );

    modelBuilder.Entity<Resource>().HasData(
        new Resource
        {
            Id = 1,
            Name = "Kütüphane",
            Type = "library",
            Capacity = 100,
            Location = "Ana Bina",
            CreatedAt = new DateTime(2025, 5, 12, 0, 0, 0, DateTimeKind.Utc)
        }
    );

    modelBuilder.Entity<Resource>().HasData(
        new Resource
        {
            Id = 2,
            Name = "Proje Odası",
            Type = "room",
            Capacity = 10,
            Location = "C Blok",
            CreatedAt = new DateTime(2025, 5, 12, 0, 0, 0, DateTimeKind.Utc)
        }
    );

    modelBuilder.Entity<Resource>().HasData(
        new Resource
        {
            Id = 3,
            Name = "Konferans Salonu",
            Type = "conference",
            Capacity = 200,
            Location = "B Blok",
            CreatedAt = new DateTime(2025, 5, 12, 0, 0, 0, DateTimeKind.Utc)
        }
    );

    modelBuilder.Entity<User>().HasData(
        new User
        {
            UserId = 1,
            FullName = "Admin Kullanıcı",
            Email = "admin@erzurum.edu.tr",
            PasswordHash = PasswordHasher.HashPassword("admin123"),
            RoleId = 1,
            CreatedAt = new DateTime(2025, 5, 12, 0, 0, 0, DateTimeKind.Utc)
        }
    );
}
    }
}