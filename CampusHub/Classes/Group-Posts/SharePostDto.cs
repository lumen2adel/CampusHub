namespace CampusHub.Classes.Group_Posts
{
    public class SharePostDto
    {
        public Guid PostId { get; set; }
        public string? SharedToGroupId { get; set; } // If null, share to personal feed
    }
}
