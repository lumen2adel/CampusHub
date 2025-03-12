namespace CampusHub.Classes.Group_Posts
{
    public class PostReport
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PostId { get; set; } // Post being reported
        public string UserId { get; set; } // Who reported it
        public string Reason { get; set; } // Reason for report
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Post Post { get; set; } // Navigation
    }
}
