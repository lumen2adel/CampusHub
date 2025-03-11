namespace CampusHub.Classes
{
    public class Notification
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ReceiverId { get; set; }  // The user receiving the notification
        public string Message { get; set; }
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
