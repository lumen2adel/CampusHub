namespace CampusHub.Classes.Group_Posts
{
    public class PostLike
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PostId { get; set; }  // Reference to the post
        public string UserId { get; set; } // Who liked the post
        public string ReactionType { get; set; } // "Like", "Heart", or "Laugh"
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Post Post { get; set; } // Navigation property
    }
}
