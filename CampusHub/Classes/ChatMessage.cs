namespace CampusHub.Classes
{
    public class ChatMessage
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string SenderId { get; set; }  // Change to string to match User ID type
        public string ReceiverId { get; set; } // Change to string to match User ID type
        public string Message { get; set; }
        public DateTime Timestamp { get; set; } = DateTime.UtcNow;
        public string Status { get; set; }
        public bool IsRead { get; set; } = false;
        public string? Reaction { get; set; }
        public DateTime? EditedAt { get; set; }
        public string? AttachmentUrl { get; set; }
        public DateTime? ReadAt { get; set; }
        public bool IsPinned { get; set; } = false;

    }

}
