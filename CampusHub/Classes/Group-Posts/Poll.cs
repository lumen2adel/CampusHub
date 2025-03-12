namespace CampusHub.Classes.Group_Posts
{
    public class Poll
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid GroupId { get; set; } // The group where the poll is created
        public string CreatedBy { get; set; } // User who created the poll
        public string Question { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime ExpiryDate { get; set; } // Poll expiration date
        public bool IsClosed { get; set; } = false;

        public ICollection<PollOption> Options { get; set; } = new List<PollOption>();
        public ICollection<PollVote> Votes { get; set; } = new List<PollVote>();
    }
}
