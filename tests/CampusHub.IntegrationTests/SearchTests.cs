using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using CampusHub.IntegrationTests.Infrastructure;

namespace CampusHub.IntegrationTests;

public class SearchTests(CampusHubApiFactory factory)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // Fields that must never appear in another user's search results
    private static readonly string[] SensitiveFields =
        ["password", "passwordResetToken", "passwordResetTokenExpiry", "email", "refreshTokens"];

    [Theory]
    [InlineData("/api/Group/SearchUsers/searchUsers?query={0}")]
    [InlineData("/api/Group/AdvancedSearch/advancedSearch?query={0}")]
    public async Task User_search_does_not_expose_credentials_of_other_users(string urlTemplate)
    {
        // Victim with a pending password reset (a live reset token in the database)
        var victimName = "Victim" + Guid.NewGuid().ToString("N")[..8];
        var victim = await TestUsers.CreateAsync(factory, victimName);
        var recover = await factory.CreateClient().PostAsJsonAsync(
            "/api/Account/RecoverPassword/recoverPassword", new { email = victim.Email }, _ct);
        recover.EnsureSuccessStatusCode();

        var attacker = await TestUsers.CreateAsync(factory, "Attacker");
        var response = await attacker.Client.GetAsync(string.Format(urlTemplate, victimName), _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        var json = await response.Content.ReadAsStringAsync(_ct);
        var found = FindUserObject(JsonDocument.Parse(json).RootElement, victim.Id);

        Assert.True(found.HasValue, $"Victim not found in search results: {json}");
        foreach (var field in SensitiveFields)
            Assert.False(found.Value.TryGetProperty(field, out _), $"Search result exposes '{field}': {found.Value}");
    }

    /// <summary>Finds the JSON object with the given "id" anywhere in the response.</summary>
    private static JsonElement? FindUserObject(JsonElement element, Guid id)
    {
        switch (element.ValueKind)
        {
            case JsonValueKind.Object:
                if (element.TryGetProperty("id", out var idProp) && idProp.ValueKind == JsonValueKind.String
                    && idProp.GetString() == id.ToString())
                    return element;
                foreach (var property in element.EnumerateObject())
                    if (FindUserObject(property.Value, id) is { } inObject) return inObject;
                break;
            case JsonValueKind.Array:
                foreach (var item in element.EnumerateArray())
                    if (FindUserObject(item, id) is { } inArray) return inArray;
                break;
        }
        return null;
    }
}
