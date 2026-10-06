using CampusHub.Classes.Group_Posts;
using CampusHub.Data;
using CampusHub.Enums;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;

namespace CampusHub.IntegrationTests.Infrastructure;

public sealed record SeededGroup(Guid GroupId, Guid FlaggedPostId);

/// <summary>Seeds domain data directly in the database, so tests only exercise the endpoint under test.</summary>
public static class TestData
{
    /// <summary>
    /// Creates a public group with <paramref name="groupAdmin"/> as a group admin
    /// (GroupMemberPages.IsAdmin) and one flagged post written by <paramref name="author"/>.
    /// </summary>
    public static async Task<SeededGroup> CreateGroupWithFlaggedPostAsync(
        CampusHubApiFactory factory, TestUser groupAdmin, TestUser author)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();

        var group = new Group
        {
            Name = "Moderation test group",
            Description = "Seeded by TestData",
            CreatedBy = groupAdmin.Id.ToString(),
            Privacy = GroupPrivacy.Public
        };
        db.Groups.Add(group);
        db.GroupMemberPages.Add(new GroupMemberPage { GroupId = group.Id, UserId = groupAdmin.Id.ToString(), IsAdmin = true });
        db.GroupMemberPages.Add(new GroupMemberPage { GroupId = group.Id, UserId = author.Id.ToString() });

        var post = new Post { UserId = author.Id, GroupId = group.Id, Description = "Reported content", IsFlagged = true };
        db.Posts.Add(post);

        await db.SaveChangesAsync(TestContext.Current.CancellationToken);
        return new SeededGroup(group.Id, post.Id);
    }

    public static async Task<bool> IsFlaggedAsync(CampusHubApiFactory factory, Guid postId)
    {
        using var scope = factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<DataContext>();
        return await db.Posts.Where(p => p.Id == postId).Select(p => p.IsFlagged)
            .SingleAsync(TestContext.Current.CancellationToken);
    }
}
