using System.Collections.Concurrent;
using campushub.Services;
using CampusHub.Services;

namespace CampusHub.IntegrationTests.Infrastructure;

public sealed class FakeEmailService : IEmailService
{
    private readonly ConcurrentDictionary<string, string> _verificationCodes = new(StringComparer.OrdinalIgnoreCase);

    public string VerificationCodeFor(string email) =>
        _verificationCodes.TryGetValue(email, out var code)
            ? code
            : throw new InvalidOperationException($"No verification code was sent to {email}.");

    public Task SendVerificationCode(string email, string code)
    {
        _verificationCodes[email] = code;
        return Task.CompletedTask;
    }

    public Task ResendVerificationCode(string email, string code) => SendVerificationCode(email, code);
    public Task SendWelcomeMessage(string email, string firstName) => Task.CompletedTask;
    public Task SendLoginWarningMessage(string email) => Task.CompletedTask;
    public Task SendPasswordRecoveryEmail(string email, string token) => Task.CompletedTask;
}

public sealed class AlwaysValidCaptcha : ICaptchaValidator
{
    public Task<bool> VerifyTokenAsync(string token) => Task.FromResult(true);
}
