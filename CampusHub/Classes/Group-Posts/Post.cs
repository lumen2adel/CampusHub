using System.ComponentModel.DataAnnotations.Schema;

namespace CampusHub.Classes.Group_Posts
{
    public class Post
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid UserId { get; set; } // User who created the post
        public Guid GroupId { get; set; } // Associated group (if any)
        public string? Description { get; set; }
        public string? MediaUrl { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // ✅ Engagement Metrics
        public int LikeCount { get; set; } = 0;
        public int CommentCount { get; set; } = 0;
        public int ShareCount { get; set; } = 0; // 🔹 Add this field
                                                 // 🟢 Full-text search field (auto-generated)
        public string SearchVector { get; set; }
        public virtual ICollection<PostLike> Likes { get; set; } = new List<PostLike>();
        public virtual ICollection<PostComment> Comments { get; set; } = new List<PostComment>();
        public bool IsFlagged { get; set; } = false; // Add this to indicate flagged posts

    }
}
