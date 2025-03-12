namespace CampusHub.Classes.Group_Posts
{
    public class ManageGroupAdminDto
    {
        public Guid GroupId { get; set; }
        public string TargetUserId { get; set; } // The member being promoted/demoted
        public bool MakeAdmin { get; set; } // True = Promote, False = Demote
    }
}
