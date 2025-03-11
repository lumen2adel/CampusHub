using CampusHub.Enums;

namespace CampusHub.JwtServices
{
    public interface IJwtTokenGenerator
    {
        string GenerateToken(Guid userId, UserRole role, string firstName, string lastName, string email);
        public string? ValidateTokenAndGetUserId(string token);
    }
}
