using campushub.Services;

namespace CampusHub.Services
{
    /// <summary>
    /// Development-only stand-in used when no SMTP credentials are configured (e.g. docker compose):
    /// e-mails are written to the log so a reviewer can register without a mail account.
    /// </summary>
    public class LoggingEmailService : IEmailService
    {
        private readonly ILogger<LoggingEmailService> _logger;

        public LoggingEmailService(ILogger<LoggingEmailService> logger) => _logger = logger;

        public Task SendVerificationCode(string email, string code) => Log(email, $"Verification code: {code}");
        public Task ResendVerificationCode(string email, string code) => Log(email, $"Verification code: {code}");
        public Task SendWelcomeMessage(string email, string firstName) => Log(email, $"Welcome, {firstName}!");
        public Task SendLoginWarningMessage(string email) => Log(email, "Login attempt warning");
        public Task SendPasswordRecoveryEmail(string email, string token) => Log(email, $"Password reset token: {token}");

        private Task Log(string email, string message)
        {
            _logger.LogWarning("[DEV e-mail, not sent] To {Email}: {Message}", email, message);
            return Task.CompletedTask;
        }
    }

    /// <summary>Development-only stand-in used when no reCAPTCHA secret is configured.</summary>
    public class DevelopmentCaptchaValidator : ICaptchaValidator
    {
        public Task<bool> VerifyTokenAsync(string token) => Task.FromResult(true);
    }
}
