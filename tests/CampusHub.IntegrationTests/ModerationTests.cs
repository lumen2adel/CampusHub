using System.Net;
using CampusHub.Enums;
using CampusHub.IntegrationTests.Infrastructure;

namespace CampusHub.IntegrationTests;

/// <summary>
/// Who may see flagged posts and unflag them?
/// Endpoints: GET  /api/Group/GetFlaggedPosts/getFlaggedPosts
///            POST /api/Group/UnflagPost/unflagPost/{postId}
/// Today ANY signed-in user can do both.
/// </summary>
public class ModerationTests(CampusHubApiFactory factory)
{
    private readonly CancellationToken _ct = TestContext.Current.CancellationToken;

    // Building blocks you can use:
    //   var user  = await TestUsers.CreateAsync(factory, "Name");                  // normal student
    //   var admin = await TestUsers.CreateAsync(factory, "Name", UserRole.Admin);  // platform admin
    //   var seeded = await TestData.CreateGroupWithFlaggedPostAsync(factory, groupAdmin, author);
    //   await user.Client.PostAsync($"/api/Group/UnflagPost/unflagPost/{seeded.FlaggedPostId}", null, _ct);
    //   await TestData.IsFlaggedAsync(factory, seeded.FlaggedPostId)

    // TODO(Laith): remove Skip and write this test first. Watch it FAIL against today's code
    // (that proves the test can detect the hole), then make it pass with your authorization rule.
    [Fact(Skip = "Laith: write this test")]
    public async Task Regular_user_cannot_unflag_a_post()
    {
        // Arrange: a flagged post, and a signed-in student who is NOT a moderator
        // Act:     the student calls unflagPost
        // Assert:  403 Forbidden, and the post is still flagged in the database
        await Task.CompletedTask;
    }

    // TODO(Laith): the "allowed" side. Without it, a rule that blocks everyone would also pass.
    [Fact(Skip = "Laith: write this test")]
    public async Task Moderator_can_unflag_a_post()
    {
        await Task.CompletedTask;
    }
}
