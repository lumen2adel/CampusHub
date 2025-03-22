namespace CampusHub.Classes.Group_Posts
{
    public class GroupAnnouncement
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GroupId { get; set; }
        public string AdminId { get; set; }
        public string Content { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public bool IsSticky { get; set; } = false;

    }
}
