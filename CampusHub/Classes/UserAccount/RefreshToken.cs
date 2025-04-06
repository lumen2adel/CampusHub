using CampusHub.Classes.UserAccount;

namespace campushub.Classes.UserAccount
{
    public class RefreshToken
    {
        public Guid Id { get; set; } = Guid.NewGuid();
        public string Token { get; set; } = string.Empty;
        public DateTime ExpiresAt { get; set; }
        public DateTime CreatedAt { get; set; } = DateTime.UtcNow;
        public DateTime? RevokedAt { get; set; }

        // FK
        public Guid UserId { get; set; }
        public AppUser User { get; set; }
        public string IpAddress { get; set; }
        public string UserAgent { get; set; }
    }

}
