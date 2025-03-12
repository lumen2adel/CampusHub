namespace CampusHub.Classes.Group_Posts
{
    public class UserSeenPost
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string UserId { get; set; } // The user who saw the post
        public Guid PostId { get; set; } // The post ID
        public DateTime SeenAt { get; set; } = DateTime.UtcNow;
    }
}
