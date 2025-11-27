using Microsoft.AspNet.SignalR;
using Online_chat.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Net.Http;
using System.Net.Http.Headers;
using Newtonsoft.Json;
using System.Configuration;
using System.Collections.Generic;
using System.Collections.Concurrent;
using System.Data.Entity;
using Microsoft.AspNet.SignalR.Hubs;

namespace Online_chat.Hubs
{
    [Microsoft.AspNet.SignalR.Authorize]
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> ConnectedUsers = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, DateTime> UserLastSeen = new ConcurrentDictionary<string, DateTime>();
        private static readonly ConcurrentDictionary<string, List<object>> _aiConversations = new ConcurrentDictionary<string, List<object>>();

        private readonly ApplicationDbContext db;

        public ChatHub()
        {
            db = new ApplicationDbContext();
        }

        // ========================================================
        // QUẢN LÝ KẾT NỐI
        // ========================================================

        public override Task OnConnected()
        {
            var username = Context.User.Identity.Name;
            var connectionId = Context.ConnectionId;

            UserHandler.ConnectedIds[connectionId] = username;
            UserHandler.UsernameToConnectionId[username] = connectionId;

            Clients.All.userConnected(username);
            Console.WriteLine($"✅ {username} connected with ConnectionId: {connectionId}");

            using (var dbContext = new ApplicationDbContext())
            {
                var currentUser = dbContext.Users.FirstOrDefault(u => u.Username == username);
                if (currentUser != null)
                {
                    var messagesToDeliver = dbContext.PrivateMessages
                        .Where(m => m.ReceiverId == currentUser.Id && m.Status == MessageStatus.Sent)
                        .ToList();

                    foreach (var message in messagesToDeliver)
                    {
                        message.Status = MessageStatus.Delivered;
                        message.DeliveredAt = DateTime.Now;

                        var sender = dbContext.Users.Find(message.SenderId);
                        if (sender != null && UserHandler.UsernameToConnectionId.TryGetValue(sender.Username, out string senderConnectionId))
                        {
                            Clients.Client(senderConnectionId).messageDelivered(message.Id, username);
                        }
                    }
                    dbContext.SaveChanges();
                }
            }

            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            var username = Context.User.Identity.Name;
            var connectionId = Context.ConnectionId;

            UserHandler.ConnectedIds.TryRemove(connectionId, out _);
            UserHandler.UsernameToConnectionId.TryRemove(username, out _);

            Clients.All.userDisconnected(username);

            Console.WriteLine($"❌ {username} disconnected");
            return base.OnDisconnected(stopCalled);
        }

        public override Task OnReconnected()
        {
            var username = Context.User.Identity.Name;
            var connectionId = Context.ConnectionId;

            if (!string.IsNullOrEmpty(username))
            {
                UserHandler.ConnectedIds[connectionId] = username;
                UserHandler.UsernameToConnectionId[username] = connectionId;

                UserLastSeen.TryRemove(username, out _);
                Clients.All.userConnected(username);

                Console.WriteLine($"🔄 {username} reconnected with ConnectionId: {connectionId}");
            }
            return base.OnReconnected();
        }

        public void GetOnlineUsers()
        {
            var onlineUsers = ConnectedUsers.Keys.ToList();
            Clients.Caller.updateOnlineUsers(onlineUsers);
        }

        public void Ping()
        {
            Clients.Caller.pong();
        }

        // ========================================================
        // GỬI TIN NHẮN NHÓM
        // ========================================================

        public void SendGroupMessage(int groupId, string messageJson)
        {
            var username = Context.User.Identity.Name;
            var timestamp = DateTime.Now;

            using (var dbContext = new ApplicationDbContext())
            {
                var sender = dbContext.Users.FirstOrDefault(u => u.Username == username);
                if (sender == null) return;

                var senderAvatar = sender.AvatarUrl ?? "/Content/default-avatar.png";

                var groupMessage = new GroupMessage
                {
                    GroupId = groupId,
                    SenderId = sender.Id,
                    Content = messageJson,
                    Timestamp = timestamp
                };
                dbContext.GroupMessages.Add(groupMessage);
                dbContext.SaveChanges();

                var groupName = $"group_{groupId}";
                Clients.Group(groupName).receiveGroupMessage(groupId, username, senderAvatar, messageJson, timestamp.ToString("o"));

                Console.WriteLine($"👥💬 Group {groupId} - {username}: {messageJson}");
            }
        }

        public void MessageDelivered(int messageId, string senderUsername)
        {
            using (var dbContext = new ApplicationDbContext())
            {
                var message = dbContext.PrivateMessages.FirstOrDefault(m => m.Id == messageId);
                if (message != null && message.Status == MessageStatus.Sent)
                {
                    message.Status = MessageStatus.Delivered;
                    message.DeliveredAt = DateTime.Now;
                    dbContext.SaveChanges();

                    if (UserHandler.UsernameToConnectionId.TryGetValue(senderUsername, out string senderConnectionId))
                    {
                        Clients.Client(senderConnectionId).messageDelivered(messageId, senderUsername);
                        Console.WriteLine($"✅ Message {messageId} delivered to {senderUsername}");
                    }
                }
            }
        }

        // ========================================================
        // GỬI TIN NHẮN PRIVATE
        // ========================================================
        public void SendPrivateMessage(string targetUsername, string messageJson, string tempId, int? parentMessageId)
        {
            var currentUsername = Context.User.Identity.Name;

            using (var dbContext = new ApplicationDbContext())
            {
                var sender = dbContext.Users.FirstOrDefault(u => u.Username == currentUsername);
                var receiver = dbContext.Users.FirstOrDefault(u => u.Username == targetUsername);

                if (sender != null && receiver != null)
                {
                    var isBlocked = dbContext.BlockedUsers.Any(b =>
                        (b.BlockerId == sender.Id && b.BlockedId == receiver.Id) ||
                        (b.BlockerId == receiver.Id && b.BlockedId == sender.Id));

                    if (isBlocked)
                    {
                        Clients.Caller.showError("Bạn không thể gửi tin nhắn cho người này vì bạn đã chặn họ hoặc bị họ chặn.");
                        return;
                    }

                    var newMessage = new PrivateMessage
                    {
                        SenderId = sender.Id,
                        ReceiverId = receiver.Id,
                        Content = messageJson,
                        Timestamp = DateTime.Now,
                        IsRead = false,
                        Status = MessageStatus.Sent,
                        ParentMessageId = parentMessageId
                    };

                    dbContext.PrivateMessages.Add(newMessage);
                    dbContext.SaveChanges();

                    ParentMessageInfo parentInfo = null;
                    if (parentMessageId.HasValue)
                    {
                        var parentMessage = dbContext.PrivateMessages
                            .Include(p => p.Sender)
                            .FirstOrDefault(p => p.Id == parentMessageId.Value);
                        if (parentMessage != null)
                        {
                            parentInfo = new ParentMessageInfo
                            {
                                Content = parentMessage.Content,
                                SenderUsername = parentMessage.Sender.Username
                            };
                        }
                    }

                    Clients.Caller.messageSent(tempId, newMessage.Id, newMessage.Timestamp);

                    if (UserHandler.UsernameToConnectionId.TryGetValue(targetUsername, out string receiverConnectionId))
                    {
                        newMessage.Status = MessageStatus.Delivered;
                        newMessage.DeliveredAt = DateTime.Now;
                        dbContext.SaveChanges();


                        Clients.Client(receiverConnectionId).receiveMessage(
                            currentUsername,
                            sender.AvatarUrl,
                            messageJson,
                            newMessage.Timestamp,
                            newMessage.Id,
                            parentInfo
                        );

                        Clients.Caller.messageDelivered(newMessage.Id, targetUsername);
                        Console.WriteLine($"✅ Message delivered: {currentUsername} → {targetUsername}");
                    }
                    else
                    {
                        Console.WriteLine($"⚠️ {targetUsername} is offline, message saved as Sent.");
                    }
                }
            }
        }


        // ========================================================
        // CHAT AI
        // ========================================================
        public async Task SendMessageToAI(string messageContent)
        {
            var senderUsername = Context.User.Identity.Name;
            if (string.IsNullOrWhiteSpace(messageContent)) return;

            var conversation = _aiConversations.GetOrAdd(senderUsername, new List<object>
            {
                new { role = "system", content = "Bạn là một trợ lý ảo thân thiện, luôn trả lời bằng cùng ngôn ngữ mà người dùng đang sử dụng." }
            });
            conversation.Add(new { role = "user", content = messageContent });

            string apiKey = ConfigurationManager.AppSettings["OpenAIApiKey"];
            if (string.IsNullOrEmpty(apiKey) || apiKey == "YOUR_KEY_HERE")
            {
                Clients.Caller.receiveAIMessage(
                    "AI Assistant",
                    "/Content/default-avatar.png",
                    JsonConvert.SerializeObject(new { type = "text", content = "Chức năng AI chưa được định cấu hình. Vui lòng cung cấp khóa API OpenAI hợp lệ trong cài đặt ứng dụng để bật tính năng này." }),
                    DateTime.UtcNow.ToString("o"),
                    null
                );
                return;
            }

            string endpoint = "https://api.openai.com/v1/chat/completions";
            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = conversation,
                temperature = 0.7
            };

            using (var client = new HttpClient())
            {
                client.DefaultRequestHeaders.Authorization = new AuthenticationHeaderValue("Bearer", apiKey);
                var jsonContent = JsonConvert.SerializeObject(requestBody);
                var content = new StringContent(jsonContent, System.Text.Encoding.UTF8, "application/json");

                try
                {
                    var response = await client.PostAsync(endpoint, content);
                    if (response.IsSuccessStatusCode)
                    {
                        var responseString = await response.Content.ReadAsStringAsync();
                        dynamic result = JsonConvert.DeserializeObject(responseString);
                        string aiReply = result.choices[0].message.content.ToString();

                        conversation.Add(new { role = "assistant", content = aiReply });

                        Clients.Caller.receiveAIMessage(
                            "AI Assistant",
                            "/Content/default-avatar.png",
                            JsonConvert.SerializeObject(new { type = "text", content = aiReply }),
                            DateTime.UtcNow.ToString("o"),
                            null
                        );
                    }
                    else
                    {
                        var errorMsg = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"❌ AI API Error: {response.StatusCode} - {errorMsg}");
                        Clients.Caller.receiveAIMessage(
                            "AI Assistant",
                            "/Content/default-avatar.png",
                            JsonConvert.SerializeObject(new { type = "text", content = "Xin lỗi, tôi đang gặp sự cố. Vui lòng thử lại sau." }),
                            DateTime.UtcNow.ToString("o"),
                            null
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ AI Exception: {ex.Message}");
                    Clients.Caller.receiveAIMessage(
                        "AI Assistant",
                        "/Content/default-avatar.png",
                        JsonConvert.SerializeObject(new { type = "text", content = $"Lỗi: {ex.Message}" }),
                        DateTime.UtcNow.ToString("o"),
                        null
                    );
                }
            }
        }

        // ========================================================
        // QUẢN LÝ NHÓM & PRIVATE CHAT
        // ========================================================

        public void JoinGroup(int groupId)
        {
            var username = Context.User.Identity.Name;
            var groupName = $"group_{groupId}";
            Groups.Add(Context.ConnectionId, groupName);
            Console.WriteLine($"👥 {username} joined group: {groupName}");
        }

        public async Task LeaveGroup(int groupId)
        {
            var currentUsername = Context.User.Identity.Name;

            using (var dbContext = new ApplicationDbContext())
            {
                var user = await dbContext.Users.FirstOrDefaultAsync(u => u.Username == currentUsername);
                if (user == null) return;

                var group = await dbContext.Groups
                                           .Include(g => g.Members.Select(m => m.User))
                                           .FirstOrDefaultAsync(g => g.Id == groupId);

                if (group == null)
                {
                    Clients.Caller.showError("Không thể tìm thấy nhóm.");
                    return;
                }

                var membership = group.Members.FirstOrDefault(m => m.UserId == user.Id);

                if (membership != null)
                {
                    dbContext.GroupMembers.Remove(membership);
                    await dbContext.SaveChangesAsync();

                    // Notify the caller immediately that they have successfully left
                    Clients.Caller.leftGroupSuccess(groupId);

                    var groupName = $"group_{groupId}";
                    var remainingUsernames = group.Members.Where(m => m.UserId != user.Id).Select(m => m.User.Username).ToList();

                    // Notify remaining members that the user has left
                    foreach (var memberUsername in remainingUsernames)
                    {
                        if (UserHandler.UsernameToConnectionId.TryGetValue(memberUsername, out string connectionId))
                        {
                            Clients.Client(connectionId).userLeftGroup(groupId, currentUsername, $"{user.DisplayName} đã rời khỏi nhóm.");
                        }
                    }

                    // Logic to disband the group if one or zero members are left
                    if (remainingUsernames.Count < 2)
                    {
                        // Delete messages for the group
                        var groupMessages = dbContext.GroupMessages.Where(gm => gm.GroupId == groupId);
                        dbContext.GroupMessages.RemoveRange(groupMessages);

                        // Delete the group itself and remaining memberships
                        var remainingMembers = dbContext.GroupMembers.Where(gm => gm.GroupId == groupId);
                        dbContext.GroupMembers.RemoveRange(remainingMembers);
                        dbContext.Groups.Remove(group);
                        await dbContext.SaveChangesAsync();

                        // Notify all members (including the one who just left) that the group is gone
                        var allMemberUsernames = remainingUsernames;
                        // The user who left is already notified by leftGroupSuccess, so they don't need the groupDisbanded notification.
                        // However, including them here doesn't hurt, as the client can handle either.

                        foreach (var memberUsername in allMemberUsernames)
                        {
                            if (UserHandler.UsernameToConnectionId.TryGetValue(memberUsername, out string connectionId))
                            {
                                Clients.Client(connectionId).groupDisbanded(groupId);
                            }
                        }
                    }
                }
            }
        }

        public void JoinPrivateGroup(string partnerUsername)
        {
            var currentUsername = Context.User.Identity.Name;
            using (var dbContext = new ApplicationDbContext())
            {
                var currentUser = dbContext.Users.FirstOrDefault(u => u.Username == currentUsername && !u.IsDeleted);
                var partnerUser = dbContext.Users.FirstOrDefault(u => u.Username == partnerUsername && !u.IsDeleted);

                if (currentUser != null && partnerUser != null)
                {
                    var groupName = GetPrivateGroupName(currentUser.Id, partnerUser.Id);
                    Groups.Add(Context.ConnectionId, groupName);
                    Console.WriteLine($"👥 {currentUsername} joined private group: {groupName}");
                }
            }
        }

        private string GetPrivateGroupName(int userId1, int userId2)
        {
            return userId1 < userId2
                ? $"private_{userId1}_{userId2}"
                : $"private_{userId2}_{userId1}";
        }

        // ========================================================
        // BLOCK/UNBLOCK USER
        // ========================================================

        public void BlockUser(string targetUsername)
        {
            var currentUsername = Context.User.Identity.Name;
            using (var dbContext = new ApplicationDbContext())
            {
                var currentUser = dbContext.Users.FirstOrDefault(u => u.Username == currentUsername);
                var targetUser = dbContext.Users.FirstOrDefault(u => u.Username == targetUsername);

                if (currentUser == null || targetUser == null)
                {
                    Clients.Caller.showError("Không tìm thấy người dùng.");
                    return;
                }

                var existingBlock = dbContext.BlockedUsers.FirstOrDefault(b => b.BlockerId == currentUser.Id && b.BlockedId == targetUser.Id);
                if (existingBlock == null)
                {
                    var newBlock = new BlockedUser { BlockerId = currentUser.Id, BlockedId = targetUser.Id };
                    dbContext.BlockedUsers.Add(newBlock);
                    dbContext.SaveChanges();
                }

                // Notify both users
                Clients.Caller.onUserBlocked(targetUsername);
                if (UserHandler.UsernameToConnectionId.TryGetValue(targetUsername, out string targetConnectionId))
                {
                    Clients.Client(targetConnectionId).onUserBlockedBy(currentUsername);
                }
            }
        }

        public void UnblockUser(string targetUsername)
        {
            var currentUsername = Context.User.Identity.Name;
            using (var dbContext = new ApplicationDbContext())
            {
                var currentUser = dbContext.Users.FirstOrDefault(u => u.Username == currentUsername);
                var targetUser = dbContext.Users.FirstOrDefault(u => u.Username == targetUsername);

                if (currentUser == null || targetUser == null)
                {
                    Clients.Caller.showError("Không tìm thấy người dùng.");
                    return;
                }

                var existingBlock = dbContext.BlockedUsers.FirstOrDefault(b => b.BlockerId == currentUser.Id && b.BlockedId == targetUser.Id);
                if (existingBlock != null)
                {
                    dbContext.BlockedUsers.Remove(existingBlock);
                    dbContext.SaveChanges();
                }

                // Notify both users
                Clients.Caller.onUserUnblocked(targetUsername);
                if (UserHandler.UsernameToConnectionId.TryGetValue(targetUsername, out string targetConnectionId))
                {
                    Clients.Client(targetConnectionId).onUserUnblockedBy(currentUsername);
                }
            }
        }


        // ========================================================
        // VOICE/VIDEO CALLS
        // ========================================================
        public void SendCallOffer(string toUsername, string offerSdp, string callType)
        {
            var fromUsername = Context.User.Identity.Name;
            if (UserHandler.UsernameToConnectionId.TryGetValue(toUsername, out string toConnectionId))
            {
                Clients.Client(toConnectionId).receiveCallOffer(fromUsername, offerSdp, callType);
                Console.WriteLine($"📞 Call offer: {fromUsername} -> {toUsername} ({callType})");
            }
        }

        public void SendCallAnswer(string toUsername, string answerSdp)
        {
            var fromUsername = Context.User.Identity.Name;
            if (UserHandler.UsernameToConnectionId.TryGetValue(toUsername, out string toConnectionId))
            {
                Clients.Client(toConnectionId).receiveCallAnswer(fromUsername, answerSdp);
                Console.WriteLine($"📞 Call answered: {fromUsername} -> {toUsername}");
            }
        }

        public void SendIceCandidate(string toUsername, string candidate)
        {
            var fromUsername = Context.User.Identity.Name;
            if (UserHandler.UsernameToConnectionId.TryGetValue(toUsername, out string toConnectionId))
            {
                Clients.Client(toConnectionId).receiveIceCandidate(fromUsername, candidate);
            }
        }

        public void EndCall(string toUsername)
        {
            var fromUsername = Context.User.Identity.Name;
            if (UserHandler.UsernameToConnectionId.TryGetValue(toUsername, out string toConnectionId))
            {
                Clients.Client(toConnectionId).callEnded(fromUsername);
                Console.WriteLine($"📞 Call ended: {fromUsername} -> {toUsername}");
            }
        }

        // ========================================================
        // FRIEND REQUEST NOTIFICATION
        // ========================================================
        public void SendFriendRequestNotification(string receiverUsername)
        {
            using (var dbContext = new ApplicationDbContext())
            {
                var receiver = dbContext.Users.FirstOrDefault(u => u.Username == receiverUsername);
                if (receiver == null) return;
                Clients.User(receiverUsername).receiveFriendRequest();
            }
        }

        // ========================================================
        // MARK MESSAGES AS READ
        // ========================================================
        public void MarkMessagesAsRead(string partnerUsername)
        {
            var currentUsername = Context.User.Identity.Name;

            using (var dbContext = new ApplicationDbContext())
            {
                var currentUser = dbContext.Users.FirstOrDefault(u => u.Username == currentUsername);
                var partner = dbContext.Users.FirstOrDefault(u => u.Username == partnerUsername);

                if (currentUser != null && partner != null)
                {
                    var unreadMessages = dbContext.PrivateMessages
                        .Where(m => m.SenderId == partner.Id & m.ReceiverId == currentUser.Id && !m.IsRead)
                        .ToList();

                    foreach (var msg in unreadMessages)
                    {
                        msg.IsRead = true;
                        msg.ReadAt = DateTime.Now;
                        msg.Status = MessageStatus.Read;
                    }

                    dbContext.SaveChanges();

                    if (UserHandler.UsernameToConnectionId.TryGetValue(partnerUsername, out string partnerConnectionId))
                    {
                        Clients.Client(partnerConnectionId).messagesMarkedAsRead(currentUsername);
                        Console.WriteLine($"✅ Notified {partnerUsername} that messages were read");
                    }
                }
            }
        }

        // ========================================================
        // TYPING INDICATOR
        // ========================================================
        public void UserTyping(string partnerUsername)
        {
            try
            {
                var currentUsername = Context.User.Identity.Name;
                using (var dbContext = new ApplicationDbContext())
                {
                    var currentUser = dbContext.Users.FirstOrDefault(u => u.Username == currentUsername && !u.IsDeleted);
                    var partnerUser = dbContext.Users.FirstOrDefault(u => u.Username == partnerUsername && !u.IsDeleted);

                    if (currentUser == null || partnerUser == null)
                    {
                        Console.WriteLine($"[ERROR] User not found: current={currentUser?.Id}, partner={partnerUser?.Id}");
                        return;
                    }

                    var groupName = GetPrivateGroupName(currentUser.Id, partnerUser.Id);
                    Clients.Group(groupName).userTyping(currentUsername);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UserTyping exception: {ex.Message}");
            }
        }

        public void UserStoppedTyping(string partnerUsername)
        {
            try
            {
                var currentUsername = Context.User.Identity.Name;
                using (var dbContext = new ApplicationDbContext())
                {
                    var currentUser = dbContext.Users.FirstOrDefault(u => u.Username == currentUsername && !u.IsDeleted);
                    var partnerUser = dbContext.Users.FirstOrDefault(u => u.Username == partnerUsername && !u.IsDeleted);

                    if (currentUser == null || partnerUser == null) return;

                    var groupName = GetPrivateGroupName(currentUser.Id, partnerUser.Id);
                    Clients.Group(groupName).userStoppedTyping(currentUsername);
                }
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UserStoppedTyping exception: {ex.Message}");
            }
        }

        // ========================================================
        // MESSAGE ACTIONS
        // ========================================================

        public void DeleteMessage(int messageId, bool deleteForEveryone)
        {
            var currentUsername = Context.User.Identity.Name;

            using (var dbContext = new ApplicationDbContext())
            {
                var user = dbContext.Users.FirstOrDefault(u => u.Username == currentUsername);
                if (user == null) return;

                var message = dbContext.PrivateMessages
                    .Include(m => m.Sender)
                    .Include(m => m.Receiver)
                    .FirstOrDefault(m => m.Id == messageId);

                if (message == null) return;

                if (deleteForEveryone && message.SenderId == user.Id)
                {
                    message.IsDeleted = true;
                    message.DeletedAt = DateTime.Now;
                    message.Content = "Tin nhắn đã được thu hồi";
                    dbContext.SaveChanges();

                    if (UserHandler.UsernameToConnectionId.TryGetValue(message.Sender.Username, out string senderConnectionId))
                    {
                        Clients.Client(senderConnectionId).onMessageDeleted(messageId, true);
                    }

                    if (UserHandler.UsernameToConnectionId.TryGetValue(message.Receiver.Username, out string receiverConnectionId))
                    {
                        Clients.Client(receiverConnectionId).onMessageDeleted(messageId, true);
                    }
                }
                else if (!deleteForEveryone)
                {
                    if (UserHandler.UsernameToConnectionId.TryGetValue(currentUsername, out string connectionId))
                    {
                        Clients.Client(connectionId).onMessageDeleted(messageId, false);
                    }
                }
            }
        }

        public void EditMessage(int messageId, string newContentJson)
        {
            var currentUsername = Context.User.Identity.Name;

            using (var dbContext = new ApplicationDbContext())
            {
                var user = dbContext.Users.FirstOrDefault(u => u.Username == currentUsername);
                if (user == null) return;

                var message = dbContext.PrivateMessages
                    .Include(m => m.Sender)
                    .Include(m => m.Receiver)
                    .FirstOrDefault(m => m.Id == messageId && m.SenderId == user.Id);

                if (message == null || message.IsDeleted) return;

                var timeElapsed = DateTime.Now - message.Timestamp;
                if (timeElapsed.TotalMinutes > 15)
                {
                    if (UserHandler.UsernameToConnectionId.TryGetValue(currentUsername, out string connectionId))
                    {
                        Clients.Client(connectionId).showError("Không thể chỉnh sửa tin nhắn sau 15 phút.");
                    }
                    return;
                }

                message.Content = newContentJson;
                message.EditedAt = DateTime.Now;
                dbContext.SaveChanges();

                if (UserHandler.UsernameToConnectionId.TryGetValue(message.Sender.Username, out string senderConnectionId))
                {
                    Clients.Client(senderConnectionId).onMessageEdited(messageId, newContentJson, message.EditedAt);
                }

                if (UserHandler.UsernameToConnectionId.TryGetValue(message.Receiver.Username, out string receiverConnectionId))
                {
                    Clients.Client(receiverConnectionId).onMessageEdited(messageId, newContentJson, message.EditedAt);
                }
            }
        }

        public void ReactToMessage(int messageId, string emoji, bool isRemoving = false)
        {
            var currentUsername = Context.User.Identity.Name;

            using (var dbContext = new ApplicationDbContext())
            {
                var user = dbContext.Users.FirstOrDefault(u => u.Username == currentUsername);
                if (user == null) return;

                var message = dbContext.PrivateMessages
                    .Include(m => m.Sender)
                    .Include(m => m.Receiver)
                    .Include(m => m.Reactions)
                    .FirstOrDefault(m => m.Id == messageId);

                if (message == null || message.IsDeleted) return;

                if (isRemoving)
                {
                    var existingReaction = message.Reactions
                        .FirstOrDefault(r => r.UserId == user.Id && r.Emoji == emoji);

                    if (existingReaction != null)
                    {
                        dbContext.MessageReactions.Remove(existingReaction);
                        dbContext.SaveChanges();
                        NotifyMessageReaction(message, user.Id, currentUsername, emoji, true);
                    }
                }
                else
                {
                    var existingReaction = message.Reactions
                        .FirstOrDefault(r => r.UserId == user.Id && r.Emoji == emoji);

                    if (existingReaction != null) return;

                    var oldReaction = message.Reactions.FirstOrDefault(r => r.UserId == user.Id);
                    if (oldReaction != null)
                    {
                        var oldEmoji = oldReaction.Emoji;
                        dbContext.MessageReactions.Remove(oldReaction);
                        dbContext.SaveChanges();
                        NotifyMessageReaction(message, user.Id, currentUsername, oldEmoji, true);
                    }

                    var newReaction = new MessageReaction
                    {
                        MessageId = messageId,
                        UserId = user.Id,
                        Emoji = emoji,
                        CreatedAt = DateTime.Now
                    };

                    dbContext.MessageReactions.Add(newReaction);
                    dbContext.SaveChanges();
                    NotifyMessageReaction(message, user.Id, currentUsername, emoji, false);
                }
            }
        }

        private void NotifyMessageReaction(PrivateMessage message, int userId, string username, string emoji, bool isRemoving)
        {
            if (UserHandler.UsernameToConnectionId.TryGetValue(message.Sender.Username, out string senderConnectionId))
            {
                Clients.Client(senderConnectionId).onMessageReacted(message.Id, userId, username, emoji, isRemoving);
            }

            if (UserHandler.UsernameToConnectionId.TryGetValue(message.Receiver.Username, out string receiverConnectionId))
            {
                Clients.Client(receiverConnectionId).onMessageReacted(message.Id, userId, username, emoji, isRemoving);
            }
        }

        // ========================================================
        // FORWARD MESSAGE
        // ========================================================
        public void ForwardMessage(int messageId, List<string> targetUsernames)
        {
            var currentUsername = Context.User.Identity.Name;

            using (var dbContext = new ApplicationDbContext())
            {
                var user = dbContext.Users.FirstOrDefault(u => u.Username == currentUsername);
                if (user == null || targetUsernames == null || !targetUsernames.Any()) return;

                var originalMessage = dbContext.PrivateMessages
                    .Include(m => m.Sender)
                    .FirstOrDefault(m => m.Id == messageId);

                if (originalMessage == null || originalMessage.IsDeleted) return;

                foreach (var targetUsername in targetUsernames)
                {
                    var receiver = dbContext.Users.FirstOrDefault(u => u.Username == targetUsername);
                    if (receiver == null) continue;

                    var forwardedMessage = new PrivateMessage
                    {
                        SenderId = user.Id,
                        ReceiverId = receiver.Id,
                        Content = originalMessage.Content,
                        MessageType = originalMessage.MessageType,
                        Timestamp = DateTime.Now,
                        Status = MessageStatus.Sent,
                        ForwardedFromId = int.TryParse(originalMessage.SenderId.ToString(), out var senderId) ? senderId : (int?)null
                    };

                    dbContext.PrivateMessages.Add(forwardedMessage);
                    dbContext.SaveChanges();

                    if (UserHandler.UsernameToConnectionId.TryGetValue(targetUsername, out string receiverConnectionId))
                    {
                        var senderAvatar = user.AvatarUrl ?? "/Content/default-avatar.png";

                        var forwarderInfo = new
                        {
                            Id = originalMessage.SenderId,
                            Username = originalMessage.Sender.Username,
                            DisplayName = originalMessage.Sender.DisplayName
                        };

                        Clients.Client(receiverConnectionId).receiveMessage(
                            user.Username,
                            senderAvatar,
                            forwardedMessage.Content,
                            forwardedMessage.Timestamp.ToString("o"),
                            forwardedMessage.Id,
                            null,
                            forwarderInfo
                        );
                    }
                }
            }
        }

        // ========================================================
        // FORWARD MESSAGE V2
        // ========================================================
        public class ForwardTarget
        {
            public string Id { get; set; }
            public string Type { get; set; }
        }

        public void ForwardMessageToTargets(int messageId, string targetsJson)
        {
            List<ForwardTarget> targets = JsonConvert.DeserializeObject<List<ForwardTarget>>(targetsJson);
            var currentUsername = Context.User.Identity.Name;

            using (var dbContext = new ApplicationDbContext())
            {
                var user = dbContext.Users.FirstOrDefault(u => u.Username == currentUsername);
                if (user == null || targets == null || !targets.Any()) return;

                var originalMessage = dbContext.PrivateMessages
                    .Include(m => m.Sender)
                    .FirstOrDefault(m => m.Id == messageId);

                if (originalMessage == null || originalMessage.IsDeleted) return;

                foreach (var target in targets)
                {
                    if (target.Type == "Private")
                    {
                        var receiver = dbContext.Users.FirstOrDefault(u => u.Username == target.Id);
                        if (receiver == null) continue;

                        var forwardedMessage = new PrivateMessage
                        {
                            SenderId = user.Id,
                            ReceiverId = receiver.Id,
                            Content = originalMessage.Content,
                            MessageType = originalMessage.MessageType,
                            Timestamp = DateTime.Now,
                            Status = MessageStatus.Sent,
                            ForwardedFromId = originalMessage.SenderId
                        };

                        dbContext.PrivateMessages.Add(forwardedMessage);
                        dbContext.SaveChanges(); // Save to get ID

                        // Notify receiver if online
                        if (UserHandler.UsernameToConnectionId.TryGetValue(receiver.Username, out string receiverConnectionId))
                        {
                            var forwarderInfo = new
                            {
                                Id = originalMessage.Sender.Id,
                                Username = originalMessage.Sender.Username,
                                DisplayName = originalMessage.Sender.DisplayName
                            };

                            Clients.Client(receiverConnectionId).receiveMessage(
                                user.Username,
                                user.AvatarUrl ?? "/Content/default-avatar.png",
                                forwardedMessage.Content,
                                forwardedMessage.Timestamp.ToString("o"),
                                forwardedMessage.Id,
                                null, // No parent message for a forward
                                forwarderInfo
                            );
                        }
                    }
                    else if (target.Type == "Group")
                    {
                        if (!int.TryParse(target.Id, out int groupId)) continue;

                        var group = dbContext.Groups.Include(g => g.Members).FirstOrDefault(g => g.Id == groupId);
                        if (group == null) continue;

                        // Workaround: Prepend forwarded info to the content
                        var originalSenderName = originalMessage.Sender.DisplayName ?? originalMessage.Sender.Username;
                        
                        var newContent = new {
                            type = "text",
                            content = $"[Chuyển tiếp từ: {originalSenderName}]"
                        };

                        // Try to parse the original content
                        try {
                            dynamic originalContentObj = JsonConvert.DeserializeObject(originalMessage.Content);
                            string originalText = originalContentObj.content;
                             newContent = new {
                                type = "text",
                                content = $"[Chuyển tiếp từ: {originalSenderName}]\n{originalText}"
                            };
                        } catch {
                            // If it's not JSON or doesn't have a 'content' property, just use the raw string.
                            newContent = new {
                                type = "text",
                                content = $"[Chuyển tiếp từ: {originalSenderName}]\n{originalMessage.Content}"
                            };
                        }

                        var groupMessage = new GroupMessage
                        {
                            GroupId = groupId,
                            SenderId = user.Id,
                            Content = JsonConvert.SerializeObject(newContent),
                            Timestamp = DateTime.Now
                        };
                        dbContext.GroupMessages.Add(groupMessage);
                        dbContext.SaveChanges();
                        
                        var groupName = $"group_{groupId}";
                        Clients.Group(groupName).receiveGroupMessage(
                            groupId, 
                            user.Username, 
                            user.AvatarUrl ?? "/Content/default-avatar.png", 
                            groupMessage.Content, 
                            groupMessage.Timestamp.ToString("o")
                        );
                    }
                }
            }
        }

        // ========================================================
        // DTO CLASSES
        // ========================================================
        public class ParentMessageInfo
        {
            public string SenderUsername { get; set; }
            public string Content { get; set; }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing && db != null)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}