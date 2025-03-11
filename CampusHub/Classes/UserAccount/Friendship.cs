using CampusHub.Helper;

namespace CampusHub.Classes.UserAccount
{
    public class Friendship : BaseEntity , IMapFrom<AppUser>
    {
        public Guid UserId { get; set; }
        public AppUser User { get; set; }

        public Guid FriendId { get; set; }
        public AppUser Friend { get; set; }
    }
}
