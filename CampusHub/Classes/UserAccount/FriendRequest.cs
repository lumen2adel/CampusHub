using CampusHub.Enums;

namespace CampusHub.Classes.UserAccount
{
    public class FriendRequest
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid SenderId { get; set; }
        public AppUser Sender { get; set; }

        public Guid ReceiverId { get; set; }
        public AppUser Receiver { get; set; }

        public FriendRequestStatus Status { get; set; } = FriendRequestStatus.Pending; // Pending, Accepted, Rejected
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }
}
