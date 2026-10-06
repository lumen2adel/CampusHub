using System.Net;
using System.Net.Http.Json;
using CampusHub.IntegrationTests.Infrastructure;

namespace CampusHub.IntegrationTests;

public class AccountTests(CampusHubApiFactory factory)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    [Fact]
    public async Task Protected_endpoint_without_token_returns_401()
    {
        var response = await factory.CreateClient().GetAsync("/api/Account/UserProfile/profile", _ct);

        Assert.Equal(HttpStatusCode.Unauthorized, response.StatusCode);
    }

    [Fact]
    public async Task Registered_user_can_log_in_and_read_their_profile()
    {
        var user = await TestUsers.CreateAsync(factory, "Login");
        var anonymous = factory.CreateClient();

        var login = await anonymous.PostAsJsonAsync("/api/Account/Login/login",
            new { email = user.Email, password = user.Password, captchaToken = "test" }, _ct);
        Assert.Equal(HttpStatusCode.OK, login.StatusCode);
        var body = await login.Content.ReadFromJsonAsync<LoginResponse>(_ct);
        Assert.False(string.IsNullOrEmpty(body?.Token));

        anonymous.DefaultRequestHeaders.Authorization = new("Bearer", body!.Token);
        var profile = await anonymous.GetAsync("/api/Account/UserProfile/profile", _ct);

        Assert.Equal(HttpStatusCode.OK, profile.StatusCode);
    }

    [Fact]
    public async Task Login_with_wrong_password_is_rejected()
    {
        var user = await TestUsers.CreateAsync(factory, "WrongPw");

        var login = await factory.CreateClient().PostAsJsonAsync("/api/Account/Login/login",
            new { email = user.Email, password = "not-the-password", captchaToken = "test" }, _ct);

        Assert.Equal(HttpStatusCode.BadRequest, login.StatusCode);
    }

    private sealed record LoginResponse(string Token);
}
