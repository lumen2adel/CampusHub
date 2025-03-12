namespace CampusHub.Classes.Group_Posts
{
    public class Event
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GroupId { get; set; } // The group where the event is created
        public string CreatedBy { get; set; } // User who created the event
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime EventDate { get; set; }
        public bool IsPrivate { get; set; } = false;
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        public ICollection<EventParticipant> Participants { get; set; } = new List<EventParticipant>();
    }
}
