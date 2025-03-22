namespace CampusHub.Classes.Group_Posts
{
    public class PostComment
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PostId { get; set; }  // Reference to the post
        public Guid UserId { get; set; } // Who commented
        public string Comment { get; set; } // The comment text
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Post Post { get; set; } // Navigation property
    }
}
