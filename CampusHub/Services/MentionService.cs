using CampusHub.Classes.Group_Posts;
using CampusHub.Data;
using System.Text.RegularExpressions;
using Microsoft.EntityFrameworkCore;

namespace CampusHub.Services
{
    public class MentionService
    {
        private readonly DataContext _dbContext;

        public MentionService(DataContext dbContext)
        {
            _dbContext = dbContext;
        }

        public async Task DetectMentions(string content, Guid postId, string senderId)
        {
            var mentionedUsernames = Regex.Matches(content, @"@(\w+)")
                .Select(m => m.Groups[1].Value.ToLower())
                .Distinct()
                .ToList();

            if (!mentionedUsernames.Any()) return;

            // ✅ Fetch Users by FirstName + LastName
            var mentionedUsers = await _dbContext.Users
                .Where(u => mentionedUsernames.Contains((u.FirstName + u.LastName).Replace(" ", "").ToLower()))
                .ToListAsync();

            foreach (var user in mentionedUsers)
            {
                // ✅ Store Mention
                _dbContext.PostMentions.Add(new PostMention
                {
                    PostId = postId,
                    MentionedUserId = user.Id.ToString()
                });

                // ✅ Send Notification
                _dbContext.Notification_Posts_Groups.Add(new Notification_Posts_Groups
                {
                    ReceiverId = user.Id.ToString(),
                    SenderId = senderId,
                    Type = "Mention",
                    Message = $"{user.FirstName} {user.LastName}, you were mentioned in a post!",
                    PostId = postId
                });
            }

            await _dbContext.SaveChangesAsync();
        }
    }
}
