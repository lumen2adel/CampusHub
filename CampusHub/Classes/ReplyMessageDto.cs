namespace CampusHub.Classes
{
    public class ReplyMessageDto
    {
        public string ReceiverId { get; set; }  // ID of the user receiving the reply
        public string Message { get; set; }     // The reply content
    }
}
