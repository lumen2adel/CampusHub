using CampusHub.Helper;

namespace CampusHub.Classes.UserAccount
{
    public class Login : IMapFrom<AppUser>
    {
        public required string? Email { get; set; }
        public required string Password { get; set; }
        public string CaptchaToken { get; set; }

    }
}
