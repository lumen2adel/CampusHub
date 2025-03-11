namespace CampusHub.Classes
{
    public class ChatMessageAttachmentDto
    {
        public string ReceiverId { get; set; }
        public string? Message { get; set; }
        public IFormFile? File { get; set; }
    }
}
