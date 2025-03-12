namespace CampusHub.Classes.Group_Posts
{
    public class CreatePollDto
    {
        public Guid GroupId { get; set; } // The group where the poll is created
        public string Question { get; set; } // The poll question
        public List<string> Options { get; set; } // List of poll options
        public DateTime ExpiryDate { get; set; } // When the poll expires
    }
}
