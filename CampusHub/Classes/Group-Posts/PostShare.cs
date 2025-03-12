namespace CampusHub.Classes.Group_Posts
{
    public class PostShare
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PostId { get; set; }  // The post being shared
        public string UserId { get; set; } // Who shared the post
        public string? SharedToGroupId { get; set; } // If null, shared to personal page
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Post Post { get; set; } // Navigation property
    }
}
