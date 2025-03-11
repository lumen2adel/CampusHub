using CampusHub.Enums;

namespace CampusHub.Classes.UserAccount
{
    public class TempUser : BaseEntity
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Password { get; set; }
        public string? Email { get; set; }
        public UserRole Role { get; set; } = UserRole.User;
        public string? VerificationCode { get; set; }
        public DateTime CodeSentAt { get; set; }
    }
}
