using System.Net.Http.Headers;
using System.Net.Http.Json;
using CampusHub.Data;
using CampusHub.Enums;
using CampusHub.JwtServices;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusHub.IntegrationTests.Infrastructure;

public sealed record TestUser(Guid Id, string Email, string Password, HttpClient Client);

public static class TestUsers
{
    public const string DefaultPassword = "Passw0rd!";

    /// <summary>
    /// Registers and verifies a user through the real API, then returns a client that sends their JWT.
    /// </summary>
    /// <remarks>
    /// The token is issued with the app's own IJwtTokenGenerator instead of calling the login
    /// endpoint: login is rate-limited to 5 requests/minute per IP, and every test client shares
    /// one "IP". Login itself is covered by a dedicated test.
    /// </remarks>
    public static async Task<TestUser> CreateAsync(
        CampusHubApiFactory factory, string firstName = "Test", UserRole role = UserRole.User)
    {
        var email = $"{firstName.ToLowerInvariant()}-{Guid.NewGuid():N}@campushub.test";
        var anonymous = factory.CreateClient();
        var ct = TestContext.Current.CancellationToken;

        var register = await anonymous.PostAsJsonAsync("/api/Account/Register/register",
            new { firstName, lastName = "User", email, password = DefaultPassword }, ct);
        register.EnsureSuccessStatusCode();

        var verify = await anonymous.PostAsJsonAsync("/api/Account/VerifyCode/verifyCode",
            new { email, code = factory.Emails.VerificationCodeFor(email) }, ct);
        verify.EnsureSuccessStatusCode();

        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        var user = await db.Users.SingleAsync(u => u.Email == email, ct);

        if (role != user.Role)
        {
            // Roles cannot be chosen at sign-up; promote directly in the database for admin scenarios
            user.Role = role;
            await db.SaveChangesAsync(ct);
        }

        var token = scope.ServiceProvider.GetRequiredService<IJwtTokenGenerator>()
            .GenerateToken(user.Id, user.Role, user.FirstName!, user.LastName!, user.Email!);

        var client = factory.CreateClient();
        client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", token);
        return new TestUser(user.Id, email, DefaultPassword, client);
    }
}
