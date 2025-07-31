namespace EtuRezervasyon.Models
{
    public class Notification {
        public int Id { get; set; }

        public int UserId { get; set; }
        public User User { get; set; } = null!;

        public int ReservationId { get; set; }
        public Reservation Reservation { get; set; } = null!;

        public string Message { get; set; } = null!;
        public DateTime SentAt { get; set; } = DateTime.Now;
    }
}