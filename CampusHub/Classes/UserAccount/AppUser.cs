using campushub.Classes.UserAccount;
using CampusHub.Enums;
using System.ComponentModel.DataAnnotations.Schema;
using System.Text.Json.Serialization;

namespace CampusHub.Classes.UserAccount
{
    public class AppUser : BaseEntity 
    {
        public string? FirstName { get; set; }
        public string? LastName { get; set; }
        // [JsonIgnore] is defense in depth: even if an AppUser is ever returned from an
        // endpoint by mistake, credentials are not serialized. Return DTOs from endpoints.
        [JsonIgnore]
        public string? Password { get; set; }
        public string? Email { get; set; }
        public UserRole Role { get; set; }
        public string UniqueIdentifier { get; set; } // Unique identifier for the user
        [JsonIgnore]
        public string? PasswordResetToken { get; set; }
        [JsonIgnore]
        public DateTime? PasswordResetTokenExpiry { get; set; }


        public ICollection<AppUser>? Friends { get; set; }
        //public string Username { get; set; }

        [NotMapped]
        public string SearchVector { get; set; }

        [JsonIgnore]
        public ICollection<RefreshToken> RefreshTokens { get; set; } = new List<RefreshToken>();

    }
}
