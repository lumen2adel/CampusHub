namespace CampusHub.Classes.Group_Posts
{
    public class PollOption
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public Guid PollId { get; set; }
        public string OptionText { get; set; }
        public int VoteCount { get; set; } = 0;

        public virtual Poll Poll { get; set; }
    }
}
