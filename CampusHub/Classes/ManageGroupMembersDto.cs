namespace CampusHub.Classes
{
    public class ManageGroupMembersDto
    {
        public Guid GroupId { get; set; }
        public List<string>? AddMembers { get; set; }
        public List<string>? RemoveMembers { get; set; }    
    }
}
