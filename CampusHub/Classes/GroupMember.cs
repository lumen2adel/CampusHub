using CampusHub.Classes.UserAccount;

namespace CampusHub.Classes
{
    public class GroupMember
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GroupId { get; set; }  // Foreign Key to GroupChat
        public Guid UserId { get; set; } // Foreign Key to AppUser


        public DateTime JoinedAt { get; set; } = DateTime.UtcNow;

        // Navigation Properties
        public virtual GroupChat Group { get; set; }
        public virtual AppUser User { get; set; }
        public bool IsAdmin { get; set; } = false;

    }
}
