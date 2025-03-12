namespace CampusHub.Classes.Group_Posts
{
    public class EventParticipant
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid EventId { get; set; }
        public string UserId { get; set; }
        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;
    }
}
