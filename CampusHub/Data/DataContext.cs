using CampusHub.Classes.UserAccount;
using CampusHub.Classes;
using Microsoft.EntityFrameworkCore;
using CampusHub.Classes.Group_Posts;
using campushub.Classes.UserAccount;

namespace CampusHub.Data
{
    public class DataContext : DbContext
    {
        public DataContext(DbContextOptions<DataContext> options) : base(options) { }
        public DbSet<AppUser> Users { get; set; }
        public DbSet<TempUser> TempUsers { get; set; }
        public DbSet<ChatMessage> ChatMessages { get; set; }
        public DbSet<Chat> Chats { get; set; }
        public DbSet<Friendship> Friendships { get; set; }
        public DbSet<FriendRequest> FriendRequests { get; set; }
        public DbSet<GroupChat> GroupChats { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<GroupMessage> GroupMessages { get; set; }
        public DbSet<Notification> Notifications { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMemberPage> GroupMemberPages { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<PostLike> PostLikes { get; set; }
        public DbSet<PostComment> PostComments { get; set; }
        public DbSet<PostShare> PostShares { get; set; }
        public DbSet<PostReport> PostReports { get; set; }
        public DbSet<Notification_Posts_Groups> Notification_Posts_Groups { get; set; }
        public DbSet<GroupJoinRequest> GroupJoinRequests { get; set; }
        public DbSet<UserSeenPost> UserSeenPosts { get; set; }
        public DbSet<ReportPost> ReportPosts { get; set; }
        public DbSet<Event> Events { get; set; }
        public DbSet<EventParticipant> EventParticipants { get; set; }
        public DbSet<Poll> Polls { get; set; }
        public DbSet<PollOption> PollOptions { get; set; }
        public DbSet<PollVote> PollVotes { get; set; }
        public DbSet<GroupAnnouncement> GroupAnnouncements { get; set; }
        public DbSet<PostMention> PostMentions { get; set; }

        public DbSet<RefreshToken> RefreshTokens { get; set; }



        protected override void OnModelCreating(ModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // ✅ Ignore the search_vector column in EF Core
            modelBuilder.Entity<Post>().Ignore(p => p.SearchVector);
            modelBuilder.Entity<Group>().Ignore(g => g.SearchVector);
            modelBuilder.Entity<AppUser>().Ignore(u => u.SearchVector);
        }

    }
}
