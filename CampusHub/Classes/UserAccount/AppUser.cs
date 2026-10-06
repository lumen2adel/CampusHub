using campushub.Classes.UserAccount;
using CampusHub.Enums;
using System.ComponentModel.DataAnnotations.Schema;

namespace CampusHub.Classes.UserAccount
{
    public class AppUser : BaseEntity 
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        public string? Password { get; set; }
        public string? Email { get; set; }
        public UserRole Role { get; set; }
        public string UniqueIdentifier { get; set; } // Unique identifier for the user
        public string? PasswordResetToken { get; set; }
        public DateTime? PasswordResetTokenExpiry { get; set; }


        public ICollection<AppUser>? Friends { get; set; }
        //public string Username { get; set; }

        [NotMapped]
        public string SearchVector { get; set; }

        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    }
}
