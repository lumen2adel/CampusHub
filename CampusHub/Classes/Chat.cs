namespace CampusHub.Classes
{
    public class Chat
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string User1Id { get; set; }
        public string User2Id { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
    }

    /// <summary>
    /// Represents a chat session between two users.
    /// </summary>
    public class ChatSession
    {
        public string User1Id { get; }
        public string User2Id { get; }
        public Guid ChatId { get; } // Add ChatId property

        public ChatSession(string user1Id, string user2Id, Guid chatId)
        {
            User1Id = user1Id;
            User2Id = user2Id;
            ChatId = chatId;
        }

        public string GetPartnerId(string userId)
        {
            return User1Id == userId ? User2Id : User1Id;
        }
    }

}
