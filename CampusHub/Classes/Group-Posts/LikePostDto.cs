namespace CampusHub.Classes.Group_Posts
{
    public class LikePostDto
    {
        public Guid PostId { get; set; }
        public string ReactionType { get; set; } // "Like", "Heart", "Laugh"
    }
}
