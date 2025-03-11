using CampusHub.Classes;
using CampusHub.Data;
using CampusHub.Hubs;
using CampusHub.Enums;
using Microsoft.AspNetCore.Authorization;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.SignalR;
using Microsoft.EntityFrameworkCore;
using System.Security.Claims;
using System.Linq;

namespace CampusHub.Controllers
{
    [ApiController]
    [Route("api/[controller]/[action]")]
    [Authorize]
    public class ChatController : ControllerBase
    {
        private readonly IHubContext<ChatHub> _hubContext;
        private readonly DataContext _dbContext;

        public ChatController(IHubContext<ChatHub> hubContext, DataContext dbContext)
        {
            _hubContext = hubContext;
            _dbContext = dbContext;
        }

        /// <summary>
        /// Sends a message using SignalR.
        /// </summary>
        [HttpPost]
        public async Task<IActionResult> SendMessage([FromBody] ChatMessageDto chatMessageDto)
        {
            var senderIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(senderIdString) || chatMessageDto == null || string.IsNullOrWhiteSpace(chatMessageDto.Message))
                return BadRequest("Invalid message data.");

            if (!Guid.TryParse(senderIdString, out Guid senderId))
            {
                return BadRequest("Invalid sender ID format.");
            }

            if (!Guid.TryParse(chatMessageDto.ReceiverId, out Guid receiverId))
            {
                return BadRequest("Invalid receiver ID format.");
            }

            if (senderId == receiverId)
                return BadRequest("You cannot send a message to yourself.");

            var receiver = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == receiverId);
            if (receiver == null)
                return NotFound("Receiver not found.");

            // ✅ Check if sender and receiver are friends
            bool isFriend = await _dbContext.FriendRequests.AnyAsync(fr =>
                (fr.SenderId == senderId && fr.ReceiverId == receiverId && fr.Status == FriendRequestStatus.Accepted) ||
                (fr.SenderId == receiverId && fr.ReceiverId == senderId && fr.Status == FriendRequestStatus.Accepted)
            );

            var trustStatus = isFriend ? "Trusted" : "Untrusted";

            // ✅ Save the message to the database with the correct status
            var savedMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                SenderId = senderId.ToString(),
                ReceiverId = receiverId.ToString(),
                Message = chatMessageDto.Message,
                Timestamp = DateTime.UtcNow,
                Status = trustStatus
            };

            await _dbContext.ChatMessages.AddAsync(savedMessage);
            await _dbContext.SaveChangesAsync();

            // ✅ Send a **new message notification** to the receiver
            await _hubContext.Clients.User(receiverId.ToString()).SendAsync("ReceiveMessageNotification", new
            {
                SenderName = $"{User.FindFirst(ClaimTypes.GivenName)?.Value} {User.FindFirst(ClaimTypes.Surname)?.Value}",
                Message = chatMessageDto.Message
            });

            // ✅ Send the actual message to the receiver via SignalR
            await _hubContext.Clients.User(receiverId.ToString()).SendAsync("ReceiveMessage", new
            {
                MessageId = savedMessage.Id,
                savedMessage.SenderId,
                savedMessage.ReceiverId,
                savedMessage.Message,
                savedMessage.Timestamp,
                Status = trustStatus
            });

            return Ok(new
            {
                Status = "Message Sent",
                MessageId = savedMessage.Id,
                TrustStatus = trustStatus
            });
        }



        [HttpGet]
        public async Task<IActionResult> GetTrustedMessages()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userIdString))
                return Unauthorized("Invalid or missing token.");

            if (!Guid.TryParse(userIdString, out Guid userId))
                return BadRequest("Invalid User ID format.");

            var trustedMessages = await _dbContext.ChatMessages
                .Where(m => (m.SenderId == userId.ToString() || m.ReceiverId == userId.ToString()) && m.Status == "Trusted")
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            return Ok(trustedMessages);
        }
        [HttpGet]
        public async Task<IActionResult> GetUntrustedMessages()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userIdString))
                return Unauthorized("Invalid or missing token.");

            if (!Guid.TryParse(userIdString, out Guid userId))
                return BadRequest("Invalid User ID format.");

            var untrustedMessages = await _dbContext.ChatMessages
                .Where(m => (m.SenderId == userId.ToString() || m.ReceiverId == userId.ToString()) && m.Status == "Untrusted")
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            return Ok(untrustedMessages);
        }
        [HttpGet]
        public async Task<IActionResult> GetUnreadMessagesCount1()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userIdString))
                return Unauthorized("Invalid or missing token.");

            if (!Guid.TryParse(userIdString, out Guid userId))
                return BadRequest("Invalid User ID format.");

            var unreadMessagesCount = await _dbContext.ChatMessages
                .Where(m => m.ReceiverId == userId.ToString() && !m.IsRead) // Assume you have an IsRead column
                .CountAsync();

            return Ok(new { UnreadMessages = unreadMessagesCount });
        }

        [HttpPatch("markMessageAsRead/{messageId}")]
        public async Task<IActionResult> MarkMessageAsRead(Guid messageId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("User ID not found in the token.");

            var message = await _dbContext.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.ReceiverId == userIdString);
            if (message == null)
                return NotFound("Message not found.");

            message.IsRead = true;
            await _dbContext.SaveChangesAsync();

            return Ok("Message marked as read.");
        }
        [HttpGet("getMessagesByUser/{userId}")]
        public async Task<IActionResult> GetMessagesByUser(Guid userId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("User ID not found in the token.");

            if (pageNumber < 1 || pageSize < 1)
                return BadRequest("Page number and page size must be greater than 0.");

            var messagesQuery = _dbContext.ChatMessages
                .Where(m => (m.SenderId == userIdString && m.ReceiverId == userId.ToString()) ||
                            (m.SenderId == userId.ToString() && m.ReceiverId == userIdString))
                .OrderByDescending(m => m.Timestamp);

            var totalMessages = await messagesQuery.CountAsync();
            var messages = await messagesQuery
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToListAsync();

            return Ok(new
            {
                TotalMessages = totalMessages,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Messages = messages
            });
        }
        [HttpDelete("deleteMessage/{messageId}")]
        public async Task<IActionResult> DeleteMessage(Guid messageId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("User ID not found in the token.");

            var message = await _dbContext.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userIdString);
            if (message == null)
                return NotFound("Message not found.");

            _dbContext.ChatMessages.Remove(message);
            await _dbContext.SaveChangesAsync();

            return Ok("Message deleted successfully.");
        }

        [HttpGet("getRecentChats")]
        public async Task<IActionResult> GetRecentChats([FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("User ID not found in the token.");

            if (!Guid.TryParse(userIdString, out Guid userId))
                return BadRequest("Invalid User ID format.");

            if (pageNumber < 1 || pageSize < 1)
                return BadRequest("Page number and page size must be greater than 0.");

            // Step 1: Fetch all messages where the user is either sender or receiver
            var messages = await _dbContext.ChatMessages
                .Where(m => m.SenderId == userIdString || m.ReceiverId == userIdString)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();  // Force fetching messages first

            // Step 2: Process grouping in-memory
            var recentChats = messages
                .GroupBy(m => m.SenderId == userIdString ? m.ReceiverId : m.SenderId)
                .Select(g => new
                {
                    ChatPartnerId = g.Key,
                    LastMessage = g.First()
                })
                .OrderByDescending(chat => chat.LastMessage.Timestamp)
                .Skip((pageNumber - 1) * pageSize)
                .Take(pageSize)
                .ToList();  // Execute in-memory filtering after grouping

            return Ok(recentChats);
        }



        [HttpGet("getNotifications")]
        public async Task<IActionResult> GetNotifications()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("User ID not found in the token.");

            if (!Guid.TryParse(userIdString, out Guid userId))
                return BadRequest("Invalid User ID format.");

            // Get unread messages
            var unreadMessages = await _dbContext.ChatMessages
                .Where(m => m.ReceiverId == userId.ToString() && !m.IsRead)
                .Select(m => new
                {
                    m.Id,
                    SenderId = m.SenderId,
                    SenderName = _dbContext.Users.Where(u => u.Id.ToString() == m.SenderId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                    m.Message,
                    m.Timestamp,
                    Type = "Message"
                })
                .ToListAsync();

            // Get pending friend requests
            var friendRequests = await _dbContext.FriendRequests
                .Where(fr => fr.ReceiverId == userId && fr.Status == FriendRequestStatus.Pending)
                .Select(fr => new
                {
                    fr.Id,
                    SenderId = fr.SenderId,
                    SenderName = _dbContext.Users.Where(u => u.Id == fr.SenderId).Select(u => u.FirstName + " " + u.LastName).FirstOrDefault(),
                    fr.CreatedAt,
                    Type = "FriendRequest"
                })
                .ToListAsync();

            return Ok(new
            {
                UnreadMessages = unreadMessages,
                FriendRequests = friendRequests
            });
        }
        [HttpGet("getUnreadMessagesCount")]
        public async Task<IActionResult> GetUnreadMessagesCount()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;

            if (string.IsNullOrWhiteSpace(userIdString))
                return Unauthorized("Invalid or missing token.");

            if (!Guid.TryParse(userIdString, out Guid userId))
                return BadRequest("Invalid User ID format.");

            var unreadMessagesCount = await _dbContext.ChatMessages
                .Where(m => m.ReceiverId == userId.ToString() && !m.IsRead)
                .CountAsync();

            return Ok(new { UnreadMessages = unreadMessagesCount });
        }
        [HttpPatch("markNotificationsAsRead")]
        public async Task<IActionResult> MarkNotificationsAsRead()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("User ID not found in the token.");

            if (!Guid.TryParse(userIdString, out Guid userId))
                return BadRequest("Invalid User ID format.");

            // Mark messages as read
            var unreadMessages = await _dbContext.ChatMessages
                .Where(m => m.ReceiverId == userId.ToString() && !m.IsRead)
                .ToListAsync();

            unreadMessages.ForEach(m => m.IsRead = true);

            await _dbContext.SaveChangesAsync();

            return Ok("All notifications marked as read.");
        }
        [HttpGet("checkChatStatus")]
        public async Task<IActionResult> CheckChatStatus()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var chat = await _dbContext.ChatMessages
                .Where(m => m.SenderId == userIdString || m.ReceiverId == userIdString)
                .OrderByDescending(m => m.Timestamp)
                .FirstOrDefaultAsync();

            if (chat == null)
                return Ok(new { Status = "Not in Chat" });

            return Ok(new
            {
                Status = "In Chat",
                ChatPartnerId = chat.SenderId == userIdString ? chat.ReceiverId : chat.SenderId
            });
        }

        //[HttpGet("checkOnlineStatus/{userId}")]
        //public async Task<IActionResult> CheckOnlineStatus(Guid userId)
        //{
        //    bool isOnline = ChatHub.ConnectedUsers.ContainsKey(userId.ToString());
        //    return Ok(new { UserId = userId, IsOnline = isOnline });
        //}
        [HttpPost("exitChat")]
        public async Task<IActionResult> ExitChat()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var chatMessages = await _dbContext.ChatMessages
                .Where(m => m.SenderId == userIdString || m.ReceiverId == userIdString)
                .ToListAsync();

            if (chatMessages.Count == 0)
                return NotFound("No active chat found.");

            _dbContext.ChatMessages.RemoveRange(chatMessages);
            await _dbContext.SaveChangesAsync();

            return Ok("Chat exited successfully.");
        }

        [HttpGet("getChatHistory/{userId}")]
        public async Task<IActionResult> GetChatHistory(Guid userId, [FromQuery] int pageNumber = 1, [FromQuery] int pageSize = 10)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("User ID not found in the token.");

            if (pageNumber < 1 || pageSize < 1)
                return BadRequest("Page number and page size must be greater than 0.");

            var messages = await _dbContext.ChatMessages
                .Where(m => (m.SenderId == userIdString && m.ReceiverId == userId.ToString()) ||
                            (m.SenderId == userId.ToString() && m.ReceiverId == userIdString))
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync(); // Fetch all messages before filtering

            var totalMessages = messages.Count; // Count after fetching
            var paginatedMessages = messages.Skip((pageNumber - 1) * pageSize).Take(pageSize).ToList();

            return Ok(new
            {
                TotalMessages = totalMessages,
                PageNumber = pageNumber,
                PageSize = pageSize,
                Messages = paginatedMessages
            });
        }

        [HttpGet("searchMessages")]
        public async Task<IActionResult> SearchMessages([FromQuery] string keyword)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            if (string.IsNullOrWhiteSpace(keyword))
                return BadRequest("Keyword cannot be empty.");

            var messages = await _dbContext.ChatMessages
                .Where(m => (m.SenderId == userIdString || m.ReceiverId == userIdString) &&
                            m.Message.Contains(keyword))
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            return Ok(messages);
        }
        [HttpDelete("deleteChatWithUser/{userId}")]
        public async Task<IActionResult> DeleteChatWithUser(Guid userId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("User ID not found in the token.");

            var messages = await _dbContext.ChatMessages
                .Where(m => (m.SenderId == userIdString && m.ReceiverId == userId.ToString()) ||
                            (m.SenderId == userId.ToString() && m.ReceiverId == userIdString))
                .ToListAsync();

            if (!messages.Any())
                return NotFound("No messages found with this user.");

            _dbContext.ChatMessages.RemoveRange(messages);
            await _dbContext.SaveChangesAsync();

            return Ok("Chat history deleted.");
        }

        [HttpPost("reactToMessage/{messageId}")]
        public async Task<IActionResult> ReactToMessage(Guid messageId, [FromBody] MessageReactionDto reactionDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var message = await _dbContext.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId);
            if (message == null)
                return NotFound("Message not found.");

            message.Reaction = reactionDto.Reaction; // Store emoji or reaction type
            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.User(message.SenderId).SendAsync("MessageReacted", new
            {
                MessageId = message.Id,
                Reaction = reactionDto.Reaction
            });

            return Ok("Reaction added.");
        }

        [HttpPatch("editMessage/{messageId}")]
        public async Task<IActionResult> EditMessage(Guid messageId, [FromBody] EditMessageDto editDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var message = await _dbContext.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userIdString);
            if (message == null)
                return NotFound("Message not found or you don't have permission to edit it.");

            message.Message = editDto.NewMessage;
            message.EditedAt = DateTime.UtcNow; // Track message edits
            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.User(message.ReceiverId).SendAsync("MessageEdited", new
            {
                MessageId = message.Id,
                NewMessage = message.Message
            });

            return Ok("Message edited successfully.");
        }
        [HttpPost("sendMessageWithAttachment")]
        public async Task<IActionResult> SendMessageWithAttachment([FromForm] ChatMessageAttachmentDto chatMessageDto)
        {
            var senderIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(senderIdString))
                return Unauthorized("Invalid or missing token.");

            if (!Guid.TryParse(senderIdString, out Guid senderId))
                return BadRequest("Invalid sender ID format.");

            if (!Guid.TryParse(chatMessageDto.ReceiverId, out Guid receiverId))
                return BadRequest("Invalid receiver ID format.");

            if (senderId == receiverId)
                return BadRequest("You cannot send a message to yourself.");

            var receiver = await _dbContext.Users.FirstOrDefaultAsync(u => u.Id == receiverId);
            if (receiver == null)
                return NotFound("Receiver not found.");

            // Upload file if provided
            string? filePath = null;
            if (chatMessageDto.File != null)
            {
                var uploadsFolder = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", "uploads");
                if (!Directory.Exists(uploadsFolder))
                    Directory.CreateDirectory(uploadsFolder);

                var fileName = $"{Guid.NewGuid()}_{chatMessageDto.File.FileName}";
                filePath = Path.Combine(uploadsFolder, fileName);

                using (var stream = new FileStream(filePath, FileMode.Create))
                {
                    await chatMessageDto.File.CopyToAsync(stream);
                }
            }

            var savedMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                SenderId = senderId.ToString(),
                ReceiverId = receiverId.ToString(),
                Message = chatMessageDto.Message,
                AttachmentUrl = filePath != null ? $"/uploads/{Path.GetFileName(filePath)}" : null,
                Timestamp = DateTime.UtcNow,
                Status = "Trusted"
            };

            await _dbContext.ChatMessages.AddAsync(savedMessage);
            await _dbContext.SaveChangesAsync();

            // Notify receiver via SignalR
            await _hubContext.Clients.User(receiverId.ToString()).SendAsync("ReceiveMessage", new
            {
                MessageId = savedMessage.Id,
                savedMessage.SenderId,
                savedMessage.ReceiverId,
                savedMessage.Message,
                savedMessage.AttachmentUrl,
                savedMessage.Timestamp
            });

            return Ok(new
            {
                Status = "Message Sent",
                MessageId = savedMessage.Id,
                AttachmentUrl = savedMessage.AttachmentUrl
            });
        }

        [HttpGet("getMessageAttachments")]
        public async Task<IActionResult> GetMessageAttachments()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var attachments = await _dbContext.ChatMessages
                .Where(m => (m.SenderId == userIdString || m.ReceiverId == userIdString) && m.AttachmentUrl != null)
                .Select(m => new
                {
                    m.Id,
                    m.AttachmentUrl,
                    m.Timestamp
                })
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            return Ok(attachments);
        }
        [HttpDelete("deleteAttachment/{messageId}")]
        public async Task<IActionResult> DeleteAttachment(Guid messageId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var message = await _dbContext.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.SenderId == userIdString);
            if (message == null)
                return NotFound("Message not found or you don't have permission to delete this attachment.");

            if (string.IsNullOrEmpty(message.AttachmentUrl))
                return BadRequest("No attachment found in this message.");

            // Delete the file from the server
            var filePath = Path.Combine(Directory.GetCurrentDirectory(), "wwwroot", message.AttachmentUrl.TrimStart('/'));
            if (System.IO.File.Exists(filePath))
                System.IO.File.Delete(filePath);

            // Remove attachment reference
            message.AttachmentUrl = null;
            await _dbContext.SaveChangesAsync();

            return Ok("Attachment deleted successfully.");
        }
        [HttpPatch("markMessageAsSeen/{messageId}")]
        public async Task<IActionResult> MarkMessageAsSeen(Guid messageId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var message = await _dbContext.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.ReceiverId == userIdString);
            if (message == null)
                return NotFound("Message not found.");

            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            // Notify sender that the message has been read
            await _hubContext.Clients.User(message.SenderId).SendAsync("MessageSeen", new
            {
                MessageId = message.Id,
                ReadAt = message.ReadAt
            });

            return Ok("Message marked as seen.");
        }
        [HttpPost("createGroup")]
        public async Task<IActionResult> CreateGroup([FromBody] CreateGroupDto groupDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            if (groupDto.MemberIds == null || !groupDto.MemberIds.Any())
                return BadRequest("At least one member is required.");

            // ✅ Ensure the creator's ID is converted properly
            if (!Guid.TryParse(userIdString, out Guid creatorId))
                return BadRequest("Invalid creator ID format.");

            // ✅ Ensure all provided users exist and are valid Guids
            var validUserIds = groupDto.MemberIds
                .Where(id => Guid.TryParse(id, out _))
                .Select(Guid.Parse)
                .ToList();

            var existingUsers = await _dbContext.Users
                .Where(u => validUserIds.Contains(u.Id))
                .Select(u => u.Id)
                .ToListAsync();

            if (existingUsers.Count != validUserIds.Count)
            {
                var missingUsers = validUserIds.Except(existingUsers).ToList();
                return BadRequest(new { Error = "Some users do not exist", MissingUsers = missingUsers });
            }

            // ✅ Create Group
            var groupChat = new GroupChat
            {
                Id = Guid.NewGuid(),
                Name = groupDto.Name,
                CreatedBy = creatorId.ToString(), // Ensure consistency with stored format
                CreatedAt = DateTime.UtcNow
            };

            _dbContext.GroupChats.Add(groupChat);
            await _dbContext.SaveChangesAsync();

            // ✅ Add Members (creator is admin by default)
            var members = existingUsers.Select(userId => new GroupMember
            {
                GroupId = groupChat.Id,
                UserId = userId,
                IsAdmin = false // Other members are not admin
            }).ToList();

            // ✅ Add creator as an admin
            members.Add(new GroupMember
            {
                GroupId = groupChat.Id,
                UserId = creatorId,
                IsAdmin = true // Creator is admin by default
            });

            await _dbContext.GroupMembers.AddRangeAsync(members);
            await _dbContext.SaveChangesAsync();

            return Ok(new { GroupId = groupChat.Id, Message = "Group chat created successfully." });
        }


        [HttpPost("sendGroupMessage")]
        public async Task<IActionResult> SendGroupMessage([FromBody] GroupMessageDto messageDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
                return Unauthorized("Invalid or missing token.");

            if (!Guid.TryParse(messageDto.GroupId.ToString(), out Guid groupId))
                return BadRequest("Invalid Group ID format.");

            // Check if the user is a member of the group
            var isMember = await _dbContext.GroupMembers
                .AnyAsync(m => m.GroupId == groupId && m.UserId == userId);

            if (!isMember)
                return BadRequest("You are not a member of this group.");

            // Ensure the sender exists in the Users table
            var senderExists = await _dbContext.Users.AnyAsync(u => u.Id == userId);
            if (!senderExists)
                return BadRequest("Sender does not exist in the users table.");

            // Create new group message
            var groupMessage = new GroupMessage
            {
                Id = Guid.NewGuid(),
                GroupId = groupId,
                SenderId = userId,  // Ensure SenderId is stored as a GUID
                Message = messageDto.Message,
                Timestamp = DateTime.UtcNow
            };

            await _dbContext.GroupMessages.AddAsync(groupMessage);
            await _dbContext.SaveChangesAsync();

            // Notify all group members except sender
            var groupMemberIds = await _dbContext.GroupMembers
                .Where(m => m.GroupId == groupId && m.UserId != userId)
                .Select(m => m.UserId.ToString())
                .ToListAsync();

            await _hubContext.Clients.Users(groupMemberIds).SendAsync("ReceiveGroupMessage", new
            {
                MessageId = groupMessage.Id,
                GroupId = groupMessage.GroupId,
                SenderId = groupMessage.SenderId.ToString(),
                Message = groupMessage.Message,
                Timestamp = groupMessage.Timestamp
            });

            return Ok(new { Message = "Message sent successfully." });
        }


        [HttpGet("getGroupMembers/{groupId}")]
        public async Task<IActionResult> GetGroupMembers(Guid groupId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
                return Unauthorized("Invalid or missing token.");

            var isMember = await _dbContext.GroupMembers
                .AnyAsync(m => m.GroupId == groupId && m.UserId == userId);

            if (!isMember)
                return Unauthorized("You are not a member of this group.");

            var members = await _dbContext.GroupMembers
                .Where(m => m.GroupId == groupId)
                .Join(
                    _dbContext.Users,
                    gm => gm.UserId,
                    u => u.Id,
                    (gm, u) => new
                    {
                        u.Id,
                        FullName = $"{u.FirstName} {u.LastName}",
                        u.Email,
                        gm.IsAdmin
                    })
                .ToListAsync();

            return Ok(members);
        }

        [HttpGet("getGroupMessages/{groupId}")]
        public async Task<IActionResult> GetGroupMessages(Guid groupId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
                return Unauthorized("Invalid or missing token.");

            var isMember = await _dbContext.GroupMembers
                .AnyAsync(m => m.GroupId == groupId && m.UserId == userId);

            if (!isMember)
                return BadRequest("You are not a member of this group.");

            var messages = await _dbContext.GroupMessages
                .Where(m => m.GroupId == groupId)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            return Ok(messages);
        }

        [HttpPost("manageGroupMembers")]
        public async Task<IActionResult> ManageGroupMembers([FromBody] ManageGroupMembersDto manageDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
                return Unauthorized("Invalid or missing token.");

            var group = await _dbContext.GroupChats.FindAsync(manageDto.GroupId);
            if (group == null)
                return NotFound("Group not found.");

            if (!Guid.TryParse(group.CreatedBy, out Guid createdBy) || createdBy != userId)
                return Unauthorized("Only the group creator can manage members.");

            // ✅ Convert memberId (string) to Guid
            var addMemberIds = manageDto.AddMembers?
                .Select(id => Guid.TryParse(id, out Guid parsedId) ? parsedId : (Guid?)null)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .ToList();

            // ✅ Add Members
            if (addMemberIds != null && addMemberIds.Any())
            {
                var existingUsers = await _dbContext.Users
                    .Where(u => addMemberIds.Contains(u.Id))
                    .Select(u => u.Id)
                    .ToListAsync();

                var newMembers = existingUsers
                    .Select(userId => new GroupMember
                    {
                        GroupId = manageDto.GroupId,
                        UserId = userId
                    })
                    .ToList();

                await _dbContext.GroupMembers.AddRangeAsync(newMembers);
            }

            // ✅ Convert removeMemberIds (string) to Guid
            var removeMemberIds = manageDto.RemoveMembers?
                .Select(id => Guid.TryParse(id, out Guid parsedId) ? parsedId : (Guid?)null)
                .Where(id => id.HasValue)
                .Select(id => id.Value)
                .ToList();

            // ✅ Remove Members
            if (removeMemberIds != null && removeMemberIds.Any())
            {
                var membersToRemove = await _dbContext.GroupMembers
                    .Where(m => m.GroupId == manageDto.GroupId && removeMemberIds.Contains(m.UserId))
                    .ToListAsync();

                _dbContext.GroupMembers.RemoveRange(membersToRemove);
            }

            await _dbContext.SaveChangesAsync();
            return Ok("Group members updated successfully.");
        }




        [HttpPost("makeUserAdmin")]
        public async Task<IActionResult> MakeUserAdmin([FromBody] GroupAdminDto adminDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
                return Unauthorized("Invalid or missing token.");

            var group = await _dbContext.GroupChats.FindAsync(adminDto.GroupId);
            if (group == null)
                return NotFound("Group not found.");

            // Convert CreatedBy (string) to Guid for comparison
            if (!Guid.TryParse(group.CreatedBy, out Guid createdBy) || createdBy != userId)
                return Unauthorized("Only the group creator can assign admins.");

            // Convert UserId (string) to Guid before searching in database
            if (!Guid.TryParse(adminDto.UserId, out Guid targetUserId))
                return BadRequest("Invalid User ID format.");

            var member = await _dbContext.GroupMembers
                .FirstOrDefaultAsync(m => m.GroupId == adminDto.GroupId && m.UserId == targetUserId);

            if (member == null)
                return NotFound("User is not a member of this group.");

            member.IsAdmin = true;
            await _dbContext.SaveChangesAsync();

            return Ok("User is now an admin.");
        }

        [HttpPost("removeUserFromGroup")]
        public async Task<IActionResult> RemoveUserFromGroup([FromBody] GroupAdminDto removeDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
                return Unauthorized("Invalid or missing token.");

            var group = await _dbContext.GroupChats.FindAsync(removeDto.GroupId);
            if (group == null)
                return NotFound("Group not found.");

            if (!Guid.TryParse(group.CreatedBy, out Guid createdBy))
                return BadRequest("Invalid creator ID format.");

            // Convert UserId (string) to Guid before searching
            if (!Guid.TryParse(removeDto.UserId, out Guid targetUserId))
                return BadRequest("Invalid User ID format.");

            var admin = await _dbContext.GroupMembers
                .FirstOrDefaultAsync(m => m.GroupId == removeDto.GroupId && m.UserId == userId);

            if (admin == null || (!admin.IsAdmin && createdBy != userId))
                return Unauthorized("Only group admins or the creator can remove members.");

            var member = await _dbContext.GroupMembers
                .FirstOrDefaultAsync(m => m.GroupId == removeDto.GroupId && m.UserId == targetUserId);

            if (member == null)
                return NotFound("User is not a member of this group.");

            _dbContext.GroupMembers.Remove(member);
            await _dbContext.SaveChangesAsync();

            return Ok("User removed from the group.");
        }

        [HttpDelete("deleteGroup/{groupId}")]
        public async Task<IActionResult> DeleteGroup(Guid groupId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var group = await _dbContext.GroupChats.Include(g => g.Messages).Include(g => g.Members).FirstOrDefaultAsync(g => g.Id == groupId);
            if (group == null)
                return NotFound("Group not found.");

            if (group.CreatedBy != userIdString)
                return Unauthorized("Only the group creator can delete this group.");

            _dbContext.GroupMessages.RemoveRange(group.Messages);
            _dbContext.GroupMembers.RemoveRange(group.Members);
            _dbContext.GroupChats.Remove(group);
            await _dbContext.SaveChangesAsync();

            return Ok("Group deleted successfully.");
        }
        [HttpPost("leaveGroup/{groupId}")]
        public async Task<IActionResult> LeaveGroup(Guid groupId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
                return Unauthorized("Invalid or missing token.");

            var member = await _dbContext.GroupMembers.FirstOrDefaultAsync(m => m.GroupId == groupId && m.UserId == userId);
            if (member == null)
                return NotFound("You are not a member of this group.");

            _dbContext.GroupMembers.Remove(member);
            await _dbContext.SaveChangesAsync();

            return Ok("You have left the group.");
        }

        [HttpPost("sendGroupMessageWithMentions")]
        public async Task<IActionResult> SendGroupMessageWithMentions([FromBody] GroupMessageDto messageDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
                return Unauthorized("Invalid or missing token.");

            var isMember = await _dbContext.GroupMembers
                .AnyAsync(m => m.GroupId == messageDto.GroupId && m.UserId == userId);

            if (!isMember)
                return BadRequest("You are not a member of this group.");

            var groupMessage = new GroupMessage
            {
                Id = Guid.NewGuid(),
                GroupId = messageDto.GroupId,
                SenderId = userId,
                Message = messageDto.Message,
                Timestamp = DateTime.UtcNow
            };

            await _dbContext.GroupMessages.AddAsync(groupMessage);
            await _dbContext.SaveChangesAsync();

            var mentionedUsers = messageDto.Message.Split(' ')
                .Where(word => word.StartsWith("@"))
                .Select(mention => mention.TrimStart('@'))
                .ToList();

            var notifiedUsers = await _dbContext.Users
                .Where(u => mentionedUsers.Contains(u.Email)) // Assuming Email as a unique identifier
                .Select(u => u.Id)
                .ToListAsync();

            foreach (var mentionedUserId in notifiedUsers)
            {
                await _hubContext.Clients.User(mentionedUserId.ToString()).SendAsync("MentionNotification", new
                {
                    GroupId = messageDto.GroupId,
                    MentionedBy = userId,
                    Message = messageDto.Message
                });
            }


            return Ok("Message sent with mentions.");
        }

        [HttpGet("unreadGroupMessages")]
        public async Task<IActionResult> GetUnreadGroupMessages()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString) || !Guid.TryParse(userIdString, out Guid userId))
                return Unauthorized("Invalid or missing token.");

            var unreadMessages = await _dbContext.GroupMessages
                .Where(m => !m.IsRead && _dbContext.GroupMembers.Any(gm => gm.GroupId == m.GroupId && gm.UserId == userId))
                .GroupBy(m => m.GroupId)
                .Select(group => new
                {
                    GroupId = group.Key,
                    UnreadCount = group.Count()
                })
                .ToListAsync();

            return Ok(unreadMessages);
        }

        [HttpGet("getAllNotifications")]
        public async Task<IActionResult> GetAllNotifications()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var notifications = await _dbContext.Notifications
                .Where(n => n.ReceiverId == userIdString && !n.IsRead)
                .OrderByDescending(n => n.CreatedAt)
                .ToListAsync();

            return Ok(notifications);
        }
        [HttpPatch("markNotificationAsRead/{notificationId}")]
        public async Task<IActionResult> MarkNotificationAsRead(Guid notificationId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var notification = await _dbContext.Notifications.FirstOrDefaultAsync(n => n.Id == notificationId && n.ReceiverId == userIdString);
            if (notification == null)
                return NotFound("Notification not found.");

            notification.IsRead = true;
            await _dbContext.SaveChangesAsync();

            return Ok("Notification marked as read.");
        }

        [HttpGet("searchUsers")]
        public async Task<IActionResult> SearchUsers([FromQuery] string query)
        {
            if (string.IsNullOrWhiteSpace(query))
                return BadRequest("Search query cannot be empty.");

            var users = await _dbContext.Users
                .Where(u => u.FirstName.Contains(query) || u.LastName.Contains(query))
                .Select(u => new
                {
                    u.Id,
                    FullName = $"{u.FirstName} {u.LastName}",
                    Email = u.Email
                })
                .ToListAsync();

            return Ok(users);
        }


        [HttpGet("filterGroups")]
        public async Task<IActionResult> FilterGroups([FromQuery] string groupName)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var groups = await _dbContext.GroupChats
                .Where(g => g.Name.Contains(groupName) && _dbContext.GroupMembers.Any(m => m.GroupId == g.Id && m.UserId.ToString() == userIdString))
                .Select(g => new
                {
                    g.Id,
                    g.Name,
                    g.CreatedAt
                })
                .ToListAsync();

            return Ok(groups);
        }

        [HttpPost("forwardMessage/{messageId}")]
        public async Task<IActionResult> ForwardMessage(Guid messageId, [FromBody] ForwardMessageDto forwardDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            if (!Guid.TryParse(userIdString, out Guid userId))
                return BadRequest("Invalid User ID format.");

            var originalMessage = await _dbContext.ChatMessages.FindAsync(messageId);
            if (originalMessage == null)
                return NotFound("Original message not found.");

            if (!Guid.TryParse(forwardDto.ReceiverId, out Guid receiverId))
                return BadRequest("Invalid receiver ID format.");

            var newMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                SenderId = userId.ToString(),
                ReceiverId = receiverId.ToString(),
                Message = $"[Forwarded] {originalMessage.Message}",
                Timestamp = DateTime.UtcNow,
                Status = "Trusted" // Assume forwarding is only allowed between trusted users
            };

            await _dbContext.ChatMessages.AddAsync(newMessage);
            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.User(receiverId.ToString()).SendAsync("ReceiveMessage", new
            {
                MessageId = newMessage.Id,
                newMessage.SenderId,
                newMessage.ReceiverId,
                newMessage.Message,
                newMessage.Timestamp
            });

            return Ok("Message forwarded successfully.");
        }
        [HttpPost("replyToMessage/{messageId}")]
        public async Task<IActionResult> ReplyToMessage(Guid messageId, [FromBody] ReplyMessageDto replyDto)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            if (!Guid.TryParse(userIdString, out Guid userId))
                return BadRequest("Invalid User ID format.");

            var originalMessage = await _dbContext.ChatMessages.FindAsync(messageId);
            if (originalMessage == null)
                return NotFound("Original message not found.");

            if (!Guid.TryParse(replyDto.ReceiverId, out Guid receiverId))
                return BadRequest("Invalid receiver ID format.");

            var newMessage = new ChatMessage
            {
                Id = Guid.NewGuid(),
                SenderId = userId.ToString(),
                ReceiverId = receiverId.ToString(),
                Message = $"[Reply to: \"{originalMessage.Message}\"] {replyDto.Message}",
                Timestamp = DateTime.UtcNow,
                Status = "Trusted"
            };

            await _dbContext.ChatMessages.AddAsync(newMessage);
            await _dbContext.SaveChangesAsync();

            await _hubContext.Clients.User(receiverId.ToString()).SendAsync("ReceiveMessage", new
            {
                MessageId = newMessage.Id,
                newMessage.SenderId,
                newMessage.ReceiverId,
                newMessage.Message,
                newMessage.Timestamp
            });

            return Ok("Reply sent successfully.");
        }
        [HttpPost("pinMessage/{messageId}")]
        public async Task<IActionResult> PinMessage(Guid messageId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var message = await _dbContext.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && (m.SenderId == userIdString || m.ReceiverId == userIdString));
            if (message == null)
                return NotFound("Message not found.");

            message.IsPinned = true;
            await _dbContext.SaveChangesAsync();

            return Ok("Message pinned successfully.");
        }
        [HttpGet("getPinnedMessages")]
        public async Task<IActionResult> GetPinnedMessages()
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var pinnedMessages = await _dbContext.ChatMessages
                .Where(m => (m.SenderId == userIdString || m.ReceiverId == userIdString) && m.IsPinned)
                .OrderByDescending(m => m.Timestamp)
                .ToListAsync();

            return Ok(pinnedMessages);
        }
        [HttpDelete("unpinMessage/{messageId}")]
        public async Task<IActionResult> UnpinMessage(Guid messageId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var message = await _dbContext.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && (m.SenderId == userIdString || m.ReceiverId == userIdString));
            if (message == null)
                return NotFound("Message not found.");

            message.IsPinned = false;
            await _dbContext.SaveChangesAsync();

            return Ok("Message unpinned successfully.");
        }

        [HttpPatch("markMessageAsSeen2/{messageId}")]
        public async Task<IActionResult> MarkMessageAsSeen2(Guid messageId)
        {
            var userIdString = User.FindFirst(ClaimTypes.NameIdentifier)?.Value;
            if (string.IsNullOrEmpty(userIdString))
                return Unauthorized("Invalid or missing token.");

            var message = await _dbContext.ChatMessages.FirstOrDefaultAsync(m => m.Id == messageId && m.ReceiverId == userIdString);
            if (message == null)
                return NotFound("Message not found.");

            message.IsRead = true;
            message.ReadAt = DateTime.UtcNow;
            await _dbContext.SaveChangesAsync();

            // Notify sender that the message has been read
            await _hubContext.Clients.User(message.SenderId).SendAsync("MessageSeen", new
            {
                MessageId = message.Id,
                ReadAt = message.ReadAt
            });

            return Ok("Message marked as seen.");
        }


    }
}
