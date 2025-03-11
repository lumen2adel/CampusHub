using CampusHub.Classes.UserAccount;

namespace CampusHub.Classes
{
    public class GroupMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GroupId { get; set; } // Foreign Key to GroupChat
        public Guid SenderId { get; set; } // User who sent the message
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual GroupChat Group { get; set; }
        public virtual AppUser Sender { get; set; }
        public bool IsRead { get; set; } = false;
    }
}
