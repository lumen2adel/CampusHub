namespace CampusHub.Classes.Group_Posts
{
    public class GroupMemberPage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GroupId { get; set; }
        public string UserId { get; set; }
        public bool IsAdmin { get; set; } = false;

        public virtual Group Group { get; set; }
    }
}
