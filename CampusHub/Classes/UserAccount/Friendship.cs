
namespace CampusHub.Classes.UserAccount
{
    public class Friendship : BaseEntity
    {
        public Guid UserId { get; set; }
        public AppUser User { get; set; }

        public Guid FriendId { get; set; }
        public AppUser Friend { get; set; }
    }
}
