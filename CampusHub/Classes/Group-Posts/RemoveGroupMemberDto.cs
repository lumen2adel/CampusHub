namespace CampusHub.Classes.Group_Posts
{
    public class RemoveGroupMemberDto
    {
        public Guid GroupId { get; set; }
        public string TargetUserId { get; set; } // The member to be removed
    }
}
