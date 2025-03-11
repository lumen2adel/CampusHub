namespace CampusHub.Classes
{
    public class GroupChat
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Name { get; set; }
        public string CreatedBy { get; set; }  // User who created the group
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual ICollection<GroupMember> Members { get; set; } = new List<GroupMember>();
        public virtual ICollection<GroupMessage> Messages { get; set; } = new List<GroupMessage>();
    }
}
