namespace EtuRezervasyon.Models
{
    public class Reservation {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int ResourceId { get; set; }
        public Resource Resource { get; set; } = null!;

        public string Status { get; set; } = "pending";
        public DateTime StartTime { get; set; }
        public DateTime EndTime { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public ICollection<Notification>? Notifications { get; set; }
    }
}