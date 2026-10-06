using System.Net;
using CampusHub.Enums;
using CampusHub.IntegrationTests.Infrastructure;

namespace CampusHub.IntegrationTests;

/// <summary>
/// Moderation endpoints are restricted to platform moderators (Admin or SuperAdmin).
/// Before the CanModerate policy, any signed-in user could list flagged posts and unflag them.
/// </summary>
public class ModerationTests(CampusHubApiFactory factory)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    private static string UnflagUrl(Guid postId) => $"/api/Group/UnflagPost/unflagPost/{postId}";
    private const string FlaggedPostsUrl = "/api/Group/GetFlaggedPosts/getFlaggedPosts";

    [Fact]
    public async Task Regular_user_cannot_unflag_a_post()
    {
        var author = await TestUsers.CreateAsync(factory, "Author");
        var student = await TestUsers.CreateAsync(factory, "Student");
        var seeded = await TestData.CreateGroupWithFlaggedPostAsync(factory, groupAdmin: author, author: author);

        var response = await student.Client.PostAsync(UnflagUrl(seeded.FlaggedPostId), null, _ct);

        // 403, not 401: the caller is authenticated, just not allowed
        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
        Assert.True(await TestData.IsFlaggedAsync(factory, seeded.FlaggedPostId), "Post must stay flagged");
    }

    [Theory]
    [InlineData(UserRole.Admin)]
    [InlineData(UserRole.SuperAdmin)]
    public async Task Moderator_can_unflag_a_post(UserRole role)
    {
        var author = await TestUsers.CreateAsync(factory, "Author");
        var moderator = await TestUsers.CreateAsync(factory, "Moderator", role);
        var seeded = await TestData.CreateGroupWithFlaggedPostAsync(factory, groupAdmin: author, author: author);

        var response = await moderator.Client.PostAsync(UnflagUrl(seeded.FlaggedPostId), null, _ct);

        Assert.Equal(HttpStatusCode.OK, response.StatusCode);
        Assert.False(await TestData.IsFlaggedAsync(factory, seeded.FlaggedPostId), "Post must be unflagged");
    }

    [Fact]
    public async Task Regular_user_cannot_list_flagged_posts()
    {
        var student = await TestUsers.CreateAsync(factory, "Student");

        var response = await student.Client.GetAsync(FlaggedPostsUrl, _ct);

        Assert.Equal(HttpStatusCode.Forbidden, response.StatusCode);
    }
}
