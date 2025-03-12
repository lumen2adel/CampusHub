namespace CampusHub.Classes.Group_Posts
{
    public class Notification_Posts_Groups
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string ReceiverId { get; set; } // User who gets the notification
        public string SenderId { get; set; } // Who triggered the notification
        public string Type { get; set; } // "Like", "Comment", "Share", "Mention"
        public string Message { get; set; } // Notification message
        public Guid? PostId { get; set; } // Optional: Related post
        public bool IsRead { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
