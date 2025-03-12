namespace CampusHub.Classes.Group_Posts
{
    public class CreateEventDto
    {
        public Guid GroupId { get; set; } // The group where the event is created
        public string CreatedBy { get; set; } // User who created the event
        public string Name { get; set; }
        public string Description { get; set; }
        public DateTime EventDate { get; set; }
        public bool IsPrivate { get; set; } = false;
    }
}
