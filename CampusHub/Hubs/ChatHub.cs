using Microsoft.AspNetCore.SignalR;
using System;
using System.Collections.Concurrent;
using System.Threading.Tasks;

namespace CampusHub.Hubs
{
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> OnlineUsers = new();



        public async Task SendTypingNotification(string receiverId, bool isTyping)
        {
            await Clients.User(receiverId).SendAsync("TypingNotification", new
            {
                SenderId = Context.UserIdentifier,
                IsTyping = isTyping
            });
        }

        public async Task SendReactionNotification(string userId, Guid messageId, string reaction)
        {
            await Clients.User(userId).SendAsync("MessageReacted", new
            {
                MessageId = messageId,
                Reaction = reaction
            });
        }

        public async Task SendEditNotification(string userId, Guid messageId, string newMessage)
        {
            await Clients.User(userId).SendAsync("MessageEdited", new
            {
                MessageId = messageId,
                NewMessage = newMessage
            });
        }

        public override async Task OnConnectedAsync()
        {
            var userId = Context.UserIdentifier;
            if (userId != null)
            {
                OnlineUsers[userId] = Context.ConnectionId;
                await Clients.All.SendAsync("UserOnline", userId);
            }
            await base.OnConnectedAsync();
        }

        public override async Task OnDisconnectedAsync(Exception exception)
        {
            var userId = Context.UserIdentifier;
            if (userId != null)
            {
                OnlineUsers.TryRemove(userId, out _);
                await Clients.All.SendAsync("UserOffline", userId);
            }
            await base.OnDisconnectedAsync(exception);
        }

        public Task GetOnlineUsers()
        {
            return Clients.Caller.SendAsync("ReceiveOnlineUsers", OnlineUsers.Keys);
        }
        /// <summary>
        /// Method called when a user sends a message.
        /// </summary>
        public async Task SendMessage(string senderId, string receiverId, string message, string status)
        {
            // Send message to the specific receiver
            await Clients.User(receiverId).SendAsync("ReceiveMessage", new
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Message = message,
                Timestamp = DateTime.UtcNow,
                Status = status
            });
        }
        public async Task SendMessageNotification(string receiverId, string senderName, string message)
        {
            await Clients.User(receiverId).SendAsync("ReceiveMessageNotification", new
            {
                SenderName = senderName,
                Message = message
            });
        }

        public async Task SendFriendRequestNotification(string receiverId, string senderName)
        {
            await Clients.User(receiverId).SendAsync("ReceiveFriendRequestNotification", new
            {
                SenderName = senderName
            });
        }
        public async Task SendReadReceiptNotification(string senderId, Guid messageId, DateTime readAt)
        {
            await Clients.User(senderId).SendAsync("MessageSeen", new
            {
                MessageId = messageId,
                ReadAt = readAt
            });
        }

        ///// <summary>
        ///// Notify when a user connects.
        ///// </summary>
        //public override async Task OnConnectedAsync()
        //{
        //    string userId = Context.UserIdentifier;
        //    if (!string.IsNullOrEmpty(userId))
        //    {
        //        await Clients.User(userId).SendAsync("UserConnected", userId);
        //    }
        //    await base.OnConnectedAsync();
        //}

        ///// <summary>
        ///// Notify when a user disconnects.
        ///// </summary>
        //public override async Task OnDisconnectedAsync(Exception exception)
        //{
        //    string userId = Context.UserIdentifier;
        //    if (!string.IsNullOrEmpty(userId))
        //    {
        //        await Clients.User(userId).SendAsync("UserDisconnected", userId);
        //    }
        //    await base.OnDisconnectedAsync(exception);
        //}
    }
}
