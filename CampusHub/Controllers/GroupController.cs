using CampusHub.Classes;
using CampusHub.Authorization;
using CampusHub.Classes.Group_Posts;
using CampusHub.Data;
using CampusHub.Enums;
using CampusHub.Services;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.EntityFrameworkCore;
using System.Linq;
using System.Security.Claims;
using System.Text.RegularExpressions;

namespace CampusHub.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    [Authorize]
    public class GroupController : ControllerBase
    {
        private readonly DataContext _dbContext;
        private readonly SearchService _searchService;

        public GroupController(DataContext dbContext, SearchService searchService)
        {
            _dbContext = dbContext;
            _searchService = searchService;
        }

        [HttpPost]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto2 groupDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var group = new Classes.Group_Posts.Group
            {
                Id = Guid.NewGuid(),
                Name = groupDto.Name,
                Description = groupDto.Description,
                CreatedBy = userId,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Groups.Add(group);
            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Group created successfully.", GroupId = group.Id });
        }

        [HttpPost("joinGroup/{groupId}")]
        public async Task<IActionResult> JoinGroup(Guid groupId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var group = await _dbContext.Groups.FindAsync(groupId);
            if (group == null) return NotFound("Group not found.");

            var existingMembership = await _dbContext.GroupMemberPages.AnyAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
            if (existingMembership) return BadRequest("You are already a member.");

            if (group.Privacy == GroupPrivacy.Public)
            {
                _dbContext.GroupMemberPages.Add(new GroupMemberPage { GroupId = groupId, UserId = userId });
                await _dbContext.SaveChangesAsync();
                return Ok("Joined group successfully.");
            }

            var existingRequest = await _dbContext.GroupJoinRequests.AnyAsync(jr => jr.GroupId == groupId && jr.UserId == userId);
            if (existingRequest) return BadRequest("Join request already sent.");

            _dbContext.GroupJoinRequests.Add(new GroupJoinRequest { GroupId = groupId, UserId = userId });
            await _dbContext.SaveChangesAsync();

            return Ok("Join request sent. Waiting for admin approval.");
        }
        [HttpPost("approveJoinRequest/{requestId}")]
        public async Task<IActionResult> ApproveJoinRequest(Guid requestId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var request = await _dbContext.GroupJoinRequests.FindAsync(requestId);
            if (request == null) return NotFound("Join request not found.");

            var isAdmin = await _dbContext.GroupMemberPages
                .AnyAsync(gm => gm.GroupId == request.GroupId && gm.UserId == userId && gm.IsAdmin);

            if (!isAdmin) return Unauthorized("Only admins can approve requests.");

            _dbContext.GroupMemberPages.Add(new GroupMemberPage { GroupId = request.GroupId, UserId = request.UserId });
            _dbContext.GroupJoinRequests.Remove(request);
            await _dbContext.SaveChangesAsync();

            return Ok("User added to the group.");
        }

        [HttpGet("homeFeed")]
        public async Task<IActionResult> GetHomeFeed()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized("Invalid token.");

            if (!Guid.TryParse(userIdString, out Guid userId)) return BadRequest("Invalid User ID format.");

            // ✅ Step 1: Get Friend IDs
            var friendIds = await _dbContext.FriendRequests
                .Where(fr => (fr.SenderId == userId || fr.ReceiverId == userId) && fr.Status == FriendRequestStatus.Accepted)
                .Select(fr => fr.SenderId == userId ? fr.ReceiverId : fr.SenderId)
                .ToListAsync();

            // ✅ Step 2: Get Seen Post IDs
            var seenPostIds = await _dbContext.UserSeenPosts
                .Where(sp => sp.UserId == userIdString)
                .Select(sp => sp.PostId)
                .ToListAsync();

            // ✅ Step 3: Fetch Friend Posts (Last 24 Hours)
            var friendPosts = await _dbContext.Posts
                .Where(p => friendIds.Contains(p.UserId) && p.CreatedAt >= DateTime.UtcNow.AddHours(-24))
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            // ✅ Step 4: Fetch Trending Posts (Last 7 Days)
            var trendingPosts = await _dbContext.Posts
                .Where(p => p.CreatedAt >= DateTime.UtcNow.AddDays(-7))
                .OrderByDescending(p => (p.LikeCount * 2) + (p.CommentCount * 3) + (p.ShareCount * 5))
                .Take(10)
                .ToListAsync();

            // ✅ Step 5: Fetch Newest Posts (Last 24 Hours)
            var newestPosts = await _dbContext.Posts
                .Where(p => p.CreatedAt >= DateTime.UtcNow.AddHours(-24) && !seenPostIds.Contains(p.Id))
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .ToListAsync();

            // ✅ Step 6: Interleave 2 Trending → 1 Newest
            var feed = new List<Post>();
            int trendingIndex = 0, newestIndex = 0;

            while (trendingIndex < trendingPosts.Count || newestIndex < newestPosts.Count)
            {
                if (trendingIndex < trendingPosts.Count)
                {
                    feed.Add(trendingPosts[trendingIndex]);
                    trendingIndex++;
                }
                if (trendingIndex < trendingPosts.Count)
                {
                    feed.Add(trendingPosts[trendingIndex]);
                    trendingIndex++;
                }
                if (newestIndex < newestPosts.Count)
                {
                    feed.Add(newestPosts[newestIndex]);
                    newestIndex++;
                }
            }

            // ✅ Step 7: Prevent duplicates and prioritize friend posts
            var finalFeed = friendPosts.Concat(feed)
                .Where(p => !seenPostIds.Contains(p.Id) || (friendIds.Contains(p.UserId) && p.CreatedAt >= DateTime.UtcNow.AddHours(-24)))
                .Distinct()
                .ToList();

            // ✅ Step 8: Fallback — if no posts found, show last 5 posts from database
            if (!finalFeed.Any())
            {
                finalFeed = await _dbContext.Posts
                    .OrderByDescending(p => p.CreatedAt)
                    .Take(5)
                    .ToListAsync();
            }

            return Ok(finalFeed);
        }


        [HttpPost("markPostAsSeen/{postId}")]
        public async Task<IActionResult> MarkPostAsSeen(Guid postId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized("Invalid token.");

            if (!Guid.TryParse(userIdString, out Guid userId)) return BadRequest("Invalid User ID format.");

            var alreadySeen = await _dbContext.UserSeenPosts
                .AnyAsync(sp => sp.UserId == userIdString && sp.PostId == postId);

            if (!alreadySeen)
            {
                _dbContext.UserSeenPosts.Add(new UserSeenPost
                {
                    UserId = userIdString,
                    PostId = postId,
                    SeenAt = DateTime.UtcNow
                });

                await _dbContext.SaveChangesAsync();
            }

            return Ok("Post marked as seen.");
        }


        [HttpDelete("{groupId}")]
        public async Task<IActionResult> LeaveGroup(Guid groupId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var membership = await _dbContext.GroupMemberPages.FirstOrDefaultAsync(gm => gm.GroupId == groupId && gm.UserId == userId);
            if (membership == null) return NotFound("You are not a member of this group.");

            _dbContext.GroupMemberPages.Remove(membership);
            await _dbContext.SaveChangesAsync();

            return Ok("Left group successfully.");
        }

        [HttpPost("createPost")]
        public async Task<IActionResult> CreatePost([FromForm] CreatePostDto postDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized("Invalid token.");
            if (!Guid.TryParse(userIdString, out Guid userId)) return BadRequest("Invalid user ID format.");

            if (!Guid.TryParse(postDto.GroupId, out Guid groupId)) return BadRequest("Invalid group ID.");

            // ✅ Detect Spam & Filter Bad Words
            if (DetectSpam(postDto.Description)) return BadRequest("Your post looks like spam.");
            postDto.Description = FilterBadWords(postDto.Description);

            string? fileUrl = null;
            if (postDto.File != null)
            {
                var uploadsFolder = Path.Combine("wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder)) Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{Guid.NewGuid()}_{postDto.File.FileName}";
                var filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await postDto.File.CopyToAsync(stream);
                }
                fileUrl = "/uploads/" + fileName;
            }

            var post = new Post
            {
                Id = Guid.NewGuid(),
                UserId = userId,
                GroupId = groupId,
                Description = postDto.Description,
                MediaUrl = fileUrl,
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.Posts.Add(post);
            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Post created successfully.", PostId = post.Id });
        }


        [HttpGet("{groupId}")]
        public async Task<IActionResult> GetGroupPosts(Guid groupId)
        {
            var posts = await _dbContext.Posts
                .Where(p => p.GroupId == groupId)
                .OrderByDescending(p => p.CreatedAt)
                .Select(p => new
                {
                    p.Id,
                    p.UserId,
                    p.Description,
                    p.MediaUrl,
                    p.CreatedAt
                })
                .ToListAsync();

            return Ok(posts);
        }

        [HttpGet]
        public async Task<IActionResult> GetUserGroups()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var groups = await _dbContext.GroupMemberPages
                .Where(gm => gm.UserId == userId)
                .Include(gm => gm.Group)
                .OrderByDescending(gm => gm.Group.CreatedAt)
                .Select(gm => new
                {
                    gm.Group.Id,
                    gm.Group.Name,
                    gm.Group.Description,
                    gm.Group.CreatedAt
                })
                .ToListAsync();

            return Ok(groups);
        }

        [HttpPost("like")]
        public async Task<IActionResult> LikePost([FromBody] LikePostDto likeDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var post = await _dbContext.Posts.FindAsync(likeDto.PostId);
            if (post == null) return NotFound("Post not found.");

            var existingLike = await _dbContext.PostLikes
                .FirstOrDefaultAsync(l => l.PostId == likeDto.PostId && l.UserId == userId);

            if (existingLike != null)
            {
                _dbContext.PostLikes.Remove(existingLike);
                post.LikeCount--;
            }
            else
            {
                var newLike = new PostLike
                {
                    PostId = likeDto.PostId,
                    UserId = userId,
                    ReactionType = likeDto.ReactionType
                };
                await _dbContext.PostLikes.AddAsync(newLike);
                post.LikeCount++;

                // ✅ Send Notification (Triggers PostgreSQL `NOTIFY`)
                await _dbContext.Notification_Posts_Groups.AddAsync(new Notification_Posts_Groups
                {
                    ReceiverId = post.UserId.ToString(),
                    SenderId = userId,
                    Type = "Like",
                    Message = "Someone liked your post!",
                    PostId = post.Id
                });
            }

            await _dbContext.SaveChangesAsync();
            return Ok("Like action updated.");
        }


        [HttpPost("comment")]
        public async Task<IActionResult> CommentPost([FromBody] CommentPostDto commentDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized("Invalid token.");
            if (!Guid.TryParse(userIdString, out Guid userId)) return BadRequest("Invalid user ID format.");


            var post = await _dbContext.Posts.FindAsync(commentDto.PostId);
            if (post == null) return NotFound("Post not found.");

            var newComment = new PostComment
            {
                PostId = commentDto.PostId,
                UserId = userId,
                Comment = commentDto.Comment
            };

            await _dbContext.PostComments.AddAsync(newComment);
            post.CommentCount++;

            // ✅ Detect Mentions
            await DetectMentions(commentDto.Comment, post.Id, userIdString);

            await _dbContext.SaveChangesAsync();
            return Ok("Comment added.");
        }

        private async Task DetectMentions(string text, Guid postId, string senderId)
        {
            var mentionedUsers = text.Split(' ')
                .Where(word => word.StartsWith("@"))
                .Select(mention => mention.TrimStart('@'))
                .ToList();

            var notifiedUsers = await _dbContext.Users
                .Where(u => mentionedUsers.Contains(u.Email)) // Assuming Email as unique identifier
                .Select(u => u.Id)
                .ToListAsync();

            foreach (var mentionedUserId in notifiedUsers)
            {
                var notification = new Notification_Posts_Groups
                {
                    ReceiverId = mentionedUserId.ToString(),
                    SenderId = senderId,
                    Type = "Mention",
                    Message = "You were mentioned in a post!",
                    PostId = postId
                };
                await _dbContext.Notification_Posts_Groups.AddAsync(notification);
            }
        }

        [HttpPost("share")]
        public async Task<IActionResult> SharePost([FromBody] SharePostDto shareDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var post = await _dbContext.Posts.FindAsync(shareDto.PostId);
            if (post == null) return NotFound("Post not found.");

            var sharedPost = new PostShare
            {
                PostId = shareDto.PostId,
                UserId = userId,
                SharedToGroupId = shareDto.SharedToGroupId.ToString()
            };

            await _dbContext.PostShares.AddAsync(sharedPost);

            // ✅ Send Notification
            var notification = new Notification_Posts_Groups
            {
                ReceiverId = post.UserId.ToString(),
                SenderId = userId,
                Type = "Share",
                Message = "Someone shared your post!",
                PostId = post.Id
            };
            await _dbContext.Notification_Posts_Groups.AddAsync(notification);

            await _dbContext.SaveChangesAsync();
            return Ok("Post shared successfully.");
        }

        [HttpPost("manageAdmin")]
        public async Task<IActionResult> ManageGroupAdmin([FromBody] ManageGroupAdminDto adminDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var group = await _dbContext.Groups.FindAsync(adminDto.GroupId);
            if (group == null) return NotFound("Group not found.");

            var requester = await _dbContext.GroupMemberPages
                .FirstOrDefaultAsync(gm => gm.GroupId == adminDto.GroupId && gm.UserId == userId);

            if (requester == null || !requester.IsAdmin) return Unauthorized("Only admins can manage members.");

            var targetMember = await _dbContext.GroupMemberPages
                .FirstOrDefaultAsync(gm => gm.GroupId == adminDto.GroupId && gm.UserId == adminDto.TargetUserId);

            if (targetMember == null) return NotFound("User is not a member of this group.");

            targetMember.IsAdmin = adminDto.MakeAdmin;
            await _dbContext.SaveChangesAsync();

            return Ok(adminDto.MakeAdmin ? "User promoted to admin." : "User demoted from admin.");
        }
        [HttpPost("removeMember")]
        public async Task<IActionResult> RemoveGroupMember([FromBody] RemoveGroupMemberDto removeDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var group = await _dbContext.Groups.FindAsync(removeDto.GroupId);
            if (group == null) return NotFound("Group not found.");

            var requester = await _dbContext.GroupMemberPages
                .FirstOrDefaultAsync(gm => gm.GroupId == removeDto.GroupId && gm.UserId == userId);

            if (requester == null || !requester.IsAdmin) return Unauthorized("Only admins can remove members.");

            var targetMember = await _dbContext.GroupMemberPages
                .FirstOrDefaultAsync(gm => gm.GroupId == removeDto.GroupId && gm.UserId == removeDto.TargetUserId);

            if (targetMember == null) return NotFound("User is not a member of this group.");

            _dbContext.GroupMemberPages.Remove(targetMember);
            await _dbContext.SaveChangesAsync();

            return Ok("User removed from the group.");
        }
        [HttpDelete("deletePost/{postId}")]
        public async Task<IActionResult> DeletePost(Guid postId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var post = await _dbContext.Posts.FindAsync(postId);
            if (post == null) return NotFound("Post not found.");

            // Check if user is post owner or an admin
            var isOwner = post.UserId.ToString() == userId;
            var isAdmin = await _dbContext.GroupMemberPages
                .AnyAsync(gm => gm.GroupId == post.GroupId && gm.UserId == userId && gm.IsAdmin);

            if (!isOwner && !isAdmin) return Unauthorized("You don't have permission to delete this post.");

            _dbContext.Posts.Remove(post);
            await _dbContext.SaveChangesAsync();

            return Ok("Post deleted.");
        }
        
        [HttpGet("getNotifications")]
        public async Task<IActionResult> GetNotifications()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var notifications = await _dbContext.Notification_Posts_Groups
                .Where(n => n.ReceiverId == userId && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return Ok(notifications);
        }

        [HttpGet("getGroupInsights/{groupId}")]
        public async Task<IActionResult> GetGroupInsights(Guid groupId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString)) return Unauthorized("Invalid token.");

            if (!Guid.TryParse(userIdString, out Guid userId)) return BadRequest("Invalid User ID format.");
            var group = await _dbContext.Groups.FindAsync(groupId);
            if (group == null) return NotFound("Group not found.");

            // ✅ Most Active Users (Users with the most posts & comments)
            var mostActiveUsers = await _dbContext.Posts
                .Where(p => p.GroupId == groupId)
                .GroupBy(p => p.UserId)
                .Select(g => new
                {
                    UserId = g.Key,
                    PostsCount = g.Count(),
                    CommentsCount = _dbContext.PostComments.Count(c => c.PostId == g.First().Id && c.UserId == g.Key)
                })
                .OrderByDescending(u => u.PostsCount + u.CommentsCount)
                .Take(5) // Get top 5 active users
                .ToListAsync();

            // ✅ Most Liked Posts (Top trending posts in the group)
            var mostLikedPosts = await _dbContext.Posts
                .Where(p => p.GroupId == groupId)
                .OrderByDescending(p => p.LikeCount)
                .Take(5)
                .Select(p => new
                {
                    p.Id,
                    p.Description,
                    p.MediaUrl,
                    p.LikeCount,
                    p.CommentCount,
                    p.CreatedAt
                })
                .ToListAsync();

            // ✅ Overall Group Engagement Stats
            var totalPosts = await _dbContext.Posts.CountAsync(p => p.GroupId == groupId);
            var totalComments = await _dbContext.PostComments.CountAsync(c => _dbContext.Posts.Any(p => p.Id == c.PostId && p.GroupId == groupId));
            var totalLikes = await _dbContext.PostLikes.CountAsync(l => _dbContext.Posts.Any(p => p.Id == l.PostId && p.GroupId == groupId));

            return Ok(new
            {
                MostActiveUsers = mostActiveUsers,
                MostLikedPosts = mostLikedPosts,
                TotalPosts = totalPosts,
                TotalComments = totalComments,
                TotalLikes = totalLikes
            });
        }
        private bool DetectSpam(string content)
        {
            // Too many links? (More than 3)
            if (Regex.Matches(content, @"http[s]?:\/\/").Count > 3) return true;

            // Repeated words? (Same word 5+ times)
            var words = content.Split(' ');
            var wordGroups = words.GroupBy(w => w).Where(g => g.Count() >= 5);
            if (wordGroups.Any()) return true;

            // Too long? (More than 500 characters)
            if (content.Length > 500) return true;

            return false;
        }
        private string FilterBadWords(string content)
        {
            List<string> badWords = new List<string> { "fuck you", "badword2", "badword3" }; // Add more
            foreach (var badWord in badWords)
            {
                content = Regex.Replace(content, $@"\b{badWord}\b", "***", RegexOptions.IgnoreCase);
            }
            return content;
        }
      
        [Authorize(Policy = Policies.CanModerate)]
        [HttpGet("getReportedPosts")]
        public async Task<IActionResult> GetReportedPosts()
        {
            var reports = await _dbContext.ReportPosts
                .Where(r => !r.Resolved)
                .Select(r => new
                {
                    r.Id,
                    r.PostId,
                    r.UserId,
                    r.Reason,
                    r.CreatedAt
                })
                .ToListAsync();

            return Ok(reports);
        }

        [HttpGet("searchPosts")]
        public async Task<IActionResult> SearchPosts([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return BadRequest("Search query cannot be empty.");

            var posts = await _searchService.SearchPostsAsync(query);
            return Ok(posts);
        }

        [HttpGet("searchGroups")]
        public async Task<IActionResult> SearchGroups([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return BadRequest("Search query cannot be empty.");

            var groups = await _searchService.SearchGroupsAsync(query);
            return Ok(groups);
        }

        [HttpGet("searchUsers")]
        public async Task<IActionResult> SearchUsers([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return BadRequest("Search query cannot be empty.");

            var users = await _searchService.SearchUsersAsync(query);
            return Ok(users);
        }
        [HttpPost("createEvent")]
        public async Task<IActionResult> CreateEvent([FromBody] CreateEventDto eventDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var group = await _dbContext.Groups.FindAsync(eventDto.GroupId);
            if (group == null) return NotFound("Group not found.");

            // ✅ Check if User is an Admin (compare string with string)
            var isAdmin = await _dbContext.GroupMemberPages
                .AnyAsync(gm => gm.GroupId == eventDto.GroupId && gm.UserId == userId && gm.IsAdmin);

            if (!isAdmin) return Unauthorized("Only admins can create events.");

            var newEvent = new Event
            {
                GroupId = eventDto.GroupId,
                CreatedBy = userId,
                Name = eventDto.Name,
                Description = eventDto.Description,
                EventDate = eventDto.EventDate,
                IsPrivate = eventDto.IsPrivate
            };

            _dbContext.Events.Add(newEvent);
            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Event created successfully.", EventId = newEvent.Id });
        }


        [HttpPost("joinEvent/{eventId}")]
        public async Task<IActionResult> JoinEvent(Guid eventId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var eventEntity = await _dbContext.Events.FindAsync(eventId);
            if (eventEntity == null) return NotFound("Event not found.");

            var alreadyJoined = await _dbContext.EventParticipants
                .AnyAsync(ep => ep.EventId == eventId && ep.UserId == userId);

            if (alreadyJoined) return BadRequest("You have already joined this event.");

            _dbContext.EventParticipants.Add(new EventParticipant { EventId = eventId, UserId = userId });
            await _dbContext.SaveChangesAsync();

            return Ok("Successfully joined the event.");
        }
        [HttpGet("getUpcomingEvents")]
        public async Task<IActionResult> GetUpcomingEvents()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            // ✅ Get Groups the User is in
            var groupIds = await _dbContext.GroupMemberPages
                .Where(gm => gm.UserId == userId)
                .Select(gm => gm.GroupId)
                .ToListAsync();

            var upcomingEvents = await _dbContext.Events
                .Where(e => groupIds.Contains(e.GroupId) && e.EventDate >= DateTime.UtcNow)
                .OrderBy(e => e.EventDate)
                .ToListAsync();

            return Ok(upcomingEvents);
        }
        [HttpPost("createPoll")]
        public async Task<IActionResult> CreatePoll([FromBody] CreatePollDto pollDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var group = await _dbContext.Groups.FindAsync(pollDto.GroupId);
            if (group == null) return NotFound("Group not found.");

            var isMember = await _dbContext.GroupMemberPages
                .AnyAsync(m => m.GroupId == pollDto.GroupId && m.UserId == userId);

            if (!isMember) return Unauthorized("Only group members can create polls.");

            var poll = new Poll
            {
                GroupId = pollDto.GroupId,
                CreatedBy = userId,
                Question = pollDto.Question,
                ExpiryDate = pollDto.ExpiryDate
            };

            _dbContext.Polls.Add(poll);
            await _dbContext.SaveChangesAsync();

            // ✅ Add poll options
            foreach (var optionText in pollDto.Options)
            {
                _dbContext.PollOptions.Add(new PollOption { PollId = poll.Id, OptionText = optionText });
            }

            await _dbContext.SaveChangesAsync();

            return Ok(new { Message = "Poll created successfully.", PollId = poll.Id });
        }
        [HttpPost("voteOnPoll/{pollId}/{optionId}")]
        public async Task<IActionResult> VoteOnPoll(Guid pollId, Guid optionId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var poll = await _dbContext.Polls.FindAsync(pollId);
            if (poll == null || poll.IsClosed) return BadRequest("Poll not available.");

            var hasVoted = await _dbContext.PollVotes
                .AnyAsync(v => v.PollId == pollId && v.UserId == userId);

            if (hasVoted) return BadRequest("You have already voted on this poll.");

            var option = await _dbContext.PollOptions.FindAsync(optionId);
            if (option == null) return NotFound("Poll option not found.");

            option.VoteCount++;

            _dbContext.PollVotes.Add(new PollVote { PollId = pollId, OptionId = optionId, UserId = userId });
            await _dbContext.SaveChangesAsync();

            return Ok("Vote submitted successfully.");
        }
        [HttpGet("getPollResults/{pollId}")]
        public async Task<IActionResult> GetPollResults(Guid pollId)
        {
            var poll = await _dbContext.Polls
                .Include(p => p.Options)
                .FirstOrDefaultAsync(p => p.Id == pollId);

            if (poll == null) return NotFound("Poll not found.");

            return Ok(new
            {
                poll.Question,
                poll.ExpiryDate,
                IsClosed = poll.IsClosed,
                Options = poll.Options.Select(o => new { o.Id, o.OptionText, o.VoteCount })
            });
        }
    
      

        [HttpGet("getTrendingPosts")]
        public async Task<IActionResult> GetTrendingPosts()
        {
            var trendingPosts = await _dbContext.Posts
                .FromSqlRaw("""SELECT * FROM trending_posts ORDER BY "Score" DESC""")
                .ToListAsync();

            return Ok(trendingPosts);
        }

        [HttpGet("getHotPosts")]
        public async Task<IActionResult> GetHotPosts()
        {
            var hotPosts = await _dbContext.Posts
                .FromSqlRaw("""SELECT * FROM hot_posts ORDER BY "Score" DESC""")
                .ToListAsync();

            return Ok(hotPosts);
        }

        [HttpPost("markNotificationAsRead/{notificationId}")]
        public async Task<IActionResult> MarkNotificationAsRead(Guid notificationId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var notification = await _dbContext.Notification_Posts_Groups
                .FirstOrDefaultAsync(n => n.Id == notificationId && n.ReceiverId == userId);

            if (notification == null) return NotFound("Notification not found.");

            notification.IsRead = true;
            await _dbContext.SaveChangesAsync();

            return Ok("Notification marked as read.");
        }
        [HttpPost("clearAllNotifications")]
        public async Task<IActionResult> ClearAllNotifications()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var notifications = await _dbContext.Notification_Posts_Groups
                .Where(n => n.ReceiverId == userId)
                .ToListAsync();

            _dbContext.Notification_Posts_Groups.RemoveRange(notifications);
            await _dbContext.SaveChangesAsync();

            return Ok("All notifications cleared.");
        }
        [HttpGet("getUnreadNotificationsCount")]
        public async Task<IActionResult> GetUnreadNotificationsCount()
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var count = await _dbContext.Notification_Posts_Groups
                .CountAsync(n => n.ReceiverId == userId && !n.IsRead);

            return Ok(new { UnreadCount = count });
        }
        [HttpPost("reportPost")]
        public async Task<IActionResult> ReportPost([FromBody] ReportPostDto reportDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized("Invalid token.");

            var post = await _dbContext.Posts.FindAsync(reportDto.PostId);
            if (post == null) return NotFound("Post not found.");

            var report = new PostReport
            {
                PostId = reportDto.PostId,
                UserId = userId,
                Reason = reportDto.Reason
            };

            await _dbContext.PostReports.AddAsync(report);

            // 🔸 Auto-flag post if reported more than 5 times
            var reportCount = await _dbContext.PostReports.CountAsync(r => r.PostId == reportDto.PostId);
            if (reportCount >= 5)
            {
                post.IsFlagged = true;
            }

            await _dbContext.SaveChangesAsync();

            return Ok("Post reported successfully.");
        }
        [Authorize(Policy = Policies.CanModerate)]
        [HttpGet("getFlaggedPosts")]
        public async Task<IActionResult> GetFlaggedPosts()
        {
            var flaggedPosts = await _dbContext.Posts
                .Where(p => p.IsFlagged)
                .OrderByDescending(p => p.CreatedAt)
                .ToListAsync();

            return Ok(flaggedPosts);
        }

        [Authorize(Policy = Policies.CanModerate)]
        [HttpPost("unflagPost/{postId}")]
        public async Task<IActionResult> UnflagPost(Guid postId)
        {
            var post = await _dbContext.Posts.FindAsync(postId);
            if (post == null) return NotFound("Post not found.");

            post.IsFlagged = false;
            await _dbContext.SaveChangesAsync();

            return Ok("Post unflagged.");
        }
        [HttpGet("advancedSearch")]
        public async Task<IActionResult> AdvancedSearch([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query)) return BadRequest("Query cannot be empty.");

            var hashtagResults = await _dbContext.Posts
                .Where(p => p.Description.Contains("#" + query))
                .ToListAsync();

            var keywordResults = await _searchService.SearchPostsAsync(query);
            var userResults = await _searchService.SearchUsersAsync(query);
            var groupResults = await _searchService.SearchGroupsAsync(query);

            return Ok(new
            {
                HashtagResults = hashtagResults,
                KeywordResults = keywordResults,
                UserResults = userResults,
                GroupResults = groupResults
            });
        }
        [HttpGet("getEventParticipants/{eventId}")]
        public async Task<IActionResult> GetEventParticipants(Guid eventId)
        {
            var participants = await _dbContext.EventParticipants
                .Where(ep => ep.EventId == eventId)
                .Select(ep => ep.UserId)
                .ToListAsync();

            return Ok(participants);
        }

        [HttpPost("removeEventParticipant/{eventId}/{userId}")]
        public async Task<IActionResult> RemoveEventParticipant(Guid eventId, string userId)
        {
            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(adminId)) return Unauthorized();

            var eventEntity = await _dbContext.Events.FindAsync(eventId);
            if (eventEntity == null) return NotFound("Event not found.");

            var isAdmin = await _dbContext.GroupMemberPages
                .AnyAsync(m => m.GroupId == eventEntity.GroupId && m.UserId == adminId && m.IsAdmin);
            if (!isAdmin) return Unauthorized("Only admins can remove participants.");

            var participant = await _dbContext.EventParticipants
                .FirstOrDefaultAsync(ep => ep.EventId == eventId && ep.UserId == userId);
            if (participant == null) return NotFound("Participant not found.");

            _dbContext.EventParticipants.Remove(participant);
            await _dbContext.SaveChangesAsync();

            return Ok("Participant removed.");
        }

        [HttpPost("cancelEvent/{eventId}")]
        public async Task<IActionResult> CancelEvent(Guid eventId)
        {
            var adminId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(adminId)) return Unauthorized();

            var eventEntity = await _dbContext.Events.FindAsync(eventId);
            if (eventEntity == null) return NotFound("Event not found.");

            var isAdmin = await _dbContext.GroupMemberPages
                .AnyAsync(m => m.GroupId == eventEntity.GroupId && m.UserId == adminId && m.IsAdmin);
            if (!isAdmin) return Unauthorized("Only admins can cancel events.");

            _dbContext.Events.Remove(eventEntity);
            await _dbContext.SaveChangesAsync();

            return Ok("Event cancelled.");
        }
        [HttpPost("closePoll/{pollId}")]
        public async Task<IActionResult> ClosePoll(Guid pollId)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var poll = await _dbContext.Polls.FindAsync(pollId);
            if (poll == null) return NotFound("Poll not found.");

            var isAdmin = await _dbContext.GroupMemberPages
                .AnyAsync(m => m.GroupId == poll.GroupId && m.UserId == userId && m.IsAdmin);
            if (!isAdmin) return Unauthorized("Only admins can close polls.");

            poll.IsClosed = true;
            await _dbContext.SaveChangesAsync();

            return Ok("Poll closed successfully.");
        }
        [HttpPost("postAnnouncement")]
        public async Task<IActionResult> PostAnnouncement([FromBody] PostAnnouncementDto announcementDto)
        {
            var userId = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userId)) return Unauthorized();

            var isAdmin = await _dbContext.GroupMemberPages
                .AnyAsync(m => m.GroupId == announcementDto.GroupId && m.UserId == userId && m.IsAdmin);
            if (!isAdmin) return Unauthorized("Only admins can post announcements.");

            var announcement = new GroupAnnouncement
            {
                GroupId = announcementDto.GroupId,
                AdminId = userId,
                Content = announcementDto.Content,
                IsSticky = announcementDto.IsSticky
            };

            _dbContext.GroupAnnouncements.Add(announcement);
            await _dbContext.SaveChangesAsync();

            return Ok("Announcement posted successfully.");
        }
        [HttpGet("getGroupAnnouncements/{groupId}")]
        public async Task<IActionResult> GetGroupAnnouncements(Guid groupId)
        {
            var announcements = await _dbContext.GroupAnnouncements
                .Where(a => a.GroupId == groupId)
                .OrderByDescending(a => a.IsSticky)
                .ThenByDescending(a => a.CreatedAt)
                .ToListAsync();

            return Ok(announcements);
        }

    }
}
