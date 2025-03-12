namespace CampusHub.Classes.Group_Posts
{
    public class PostAnnouncementDto
    {
        public Guid GroupId { get; set; } // The group where the announcement is posted
        public string Content { get; set; } // Announcement text content
    }
}
