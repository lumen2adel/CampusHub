namespace CampusHub.Services
{
    /// <summary>Sends account-related e-mails. Abstracted so tests can capture messages instead of using SMTP.</summary>
    public interface IEmailService
    {
        Task SendWelcomeMessage(string email, string firstName);
        Task SendLoginWarningMessage(string email);
        Task SendVerificationCode(string email, string code);
        Task ResendVerificationCode(string email, string code);
        Task SendPasswordRecoveryEmail(string email, string token);
    }
}
