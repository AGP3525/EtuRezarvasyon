using System;

namespace EtuRezervasyon.Models
{
    public class Resource {
        public int Id { get; set; }
        public string Name { get; set; } = null!;
        public string Type { get; set; } = null!;
        public int? Capacity { get; set; }
        public string? Location { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Reservation>? Reservations { get; set; }
    }
}