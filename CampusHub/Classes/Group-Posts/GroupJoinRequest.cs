namespace CampusHub.Classes.Group_Posts
{
    public class GroupJoinRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GroupId { get; set; }
        public string UserId { get; set; }
        public bool IsApproved { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public virtual Group Group { get; set; }
    }
}
