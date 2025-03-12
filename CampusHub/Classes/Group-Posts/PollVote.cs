namespace CampusHub.Classes.Group_Posts
{
    public class PollVote
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PollId { get; set; }
        public Guid OptionId { get; set; }
        public string UserId { get; set; }
        public DateTime VotedAt { get; set; } = DateTime.UtcNow;

        public virtual Poll Poll { get; set; }
        public virtual PollOption Option { get; set; }
    }
}
