namespace CampusHub.Classes.Group_Posts
{
    public class ReportPost
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PostId { get; set; }
        public string UserId { get; set; }
        public string Reason { get; set; }
        public bool Resolved { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Post Post { get; set; }
    }
}
