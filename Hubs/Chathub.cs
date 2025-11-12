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

namespace Online_chat.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private static readonly ConcurrentDictionary<string, string> ConnectedUsers = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, DateTime> UserLastSeen = new ConcurrentDictionary<string, DateTime>();
        private static readonly ConcurrentDictionary<string, List<object>> _aiConversations = new ConcurrentDictionary<string, List<object>>();

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

            // Logic to mark messages as Delivered when user comes online
            using (var db = new ApplicationDbContext())
            {
                var currentUser = db.Users.FirstOrDefault(u => u.Username == username);
                if (currentUser != null)
                {
                    var messagesToDeliver = db.PrivateMessages
                        .Where(m => m.ReceiverId == currentUser.Id && m.Status == MessageStatus.Sent)
                        .ToList();

                    foreach (var message in messagesToDeliver)
                    {
                        message.Status = MessageStatus.Delivered;
                        message.DeliveredAt = DateTime.Now;

                        // Notify the sender that the message has been delivered
                        var sender = db.Users.Find(message.SenderId);
                        if (sender != null && UserHandler.UsernameToConnectionId.TryGetValue(sender.Username, out string senderConnectionId))
                        {
                            Clients.Client(senderConnectionId).messageDelivered(message.Id, username);
                        }
                    }
                    db.SaveChanges();
                }
            }

            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            var username = Context.User.Identity.Name;
            var connectionId = Context.ConnectionId;

            // Xóa mapping
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

            using (var db = new ApplicationDbContext())
            {
                var sender = db.Users.FirstOrDefault(u => u.Username == username);
                if (sender == null) return;

                var senderAvatar = sender.AvatarUrl ?? "/Content/default-avatar.png";

                var groupMessage = new GroupMessage
                {
                    GroupId = groupId,
                    SenderId = sender.Id,
                    Content = messageJson,
                    Timestamp = timestamp
                };
                db.GroupMessages.Add(groupMessage);
                db.SaveChanges();

                var groupName = $"group_{groupId}";
                Clients.Group(groupName).receiveGroupMessage(groupId, username, senderAvatar, messageJson, timestamp.ToString("o"));

                Console.WriteLine($"👥💬 Group {groupId} - {username}: {messageJson}");
            }
        }

        public void MessageDelivered(int messageId, string senderUsername)
        {
            using (var db = new ApplicationDbContext())
            {
                var message = db.PrivateMessages.FirstOrDefault(m => m.Id == messageId);
                if (message != null && message.Status == MessageStatus.Sent)
                {
                    message.Status = MessageStatus.Delivered;
                    message.DeliveredAt = DateTime.Now;
                    db.SaveChanges();

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
        public void SendPrivateMessage(string targetUsername, string messageJson, string tempId)
        {
            var currentUsername = Context.User.Identity.Name;

            using (var db = new ApplicationDbContext())
            {
                var sender = db.Users.FirstOrDefault(u => u.Username == currentUsername);
                var receiver = db.Users.FirstOrDefault(u => u.Username == targetUsername);

                if (sender != null && receiver != null)
                {
                    var newMessage = new PrivateMessage
                    {
                        SenderId = sender.Id,
                        ReceiverId = receiver.Id,
                        Content = messageJson,
                        Timestamp = DateTime.Now,
                        IsRead = false,
                        Status = MessageStatus.Sent // Default status
                    };

                    db.PrivateMessages.Add(newMessage);
                    db.SaveChanges(); // Save once to get the ID

                    // Notify the sender that the message has been sent successfully
                    Clients.Caller.messageSent(tempId, newMessage.Id, newMessage.Timestamp);

                    if (UserHandler.UsernameToConnectionId.TryGetValue(targetUsername, out string receiverConnectionId))
                    {
                        // Receiver is online, so the message is delivered.
                        newMessage.Status = MessageStatus.Delivered;
                        newMessage.DeliveredAt = DateTime.Now;
                        db.SaveChanges(); // Save status update

                        // Notify receiver of the new message
                        Clients.Client(receiverConnectionId).receiveMessage(
                            currentUsername,
                            sender.AvatarUrl,
                            messageJson,
                            newMessage.Timestamp,
                            newMessage.Id
                        );

                        // Notify sender that the message was delivered
                        Clients.Caller.messageDelivered(newMessage.Id, targetUsername);
                        Console.WriteLine($"✅ Message delivered: {currentUsername} → {targetUsername}");
                    }
                    else
                    {
                        // Receiver is offline, status remains 'Sent'.
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
            if (string.IsNullOrEmpty(apiKey))
            {
                Clients.Caller.receiveMessage(
                    "AI Assistant",
                    "/Content/default-avatar.png",
                    JsonConvert.SerializeObject(new { type = "text", content = "Xin lỗi, API AI chưa được cấu hình." }),
                    DateTime.UtcNow.ToString("o")
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

                        Clients.Caller.receiveMessage(
                            "AI Assistant",
                            "/Content/default-avatar.png",
                            JsonConvert.SerializeObject(new { type = "text", content = aiReply }),
                            DateTime.UtcNow.ToString("o")
                        );
                    }
                    else
                    {
                        var errorMsg = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"❌ AI API Error: {response.StatusCode} - {errorMsg}");
                        Clients.Caller.receiveMessage(
                            "AI Assistant",
                            "/Content/default-avatar.png",
                            JsonConvert.SerializeObject(new { type = "text", content = "Xin lỗi, tôi đang gặp sự cố. Vui lòng thử lại sau." }),
                            DateTime.UtcNow.ToString("o")
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ AI Exception: {ex.Message}");
                    Clients.Caller.receiveMessage(
                        "AI Assistant",
                        "/Content/default-avatar.png",
                        JsonConvert.SerializeObject(new { type = "text", content = $"Lỗi: {ex.Message}" }),
                        DateTime.UtcNow.ToString("o")
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

        public void LeaveGroup(int groupId)
        {
            var groupName = $"group_{groupId}";
            Groups.Remove(Context.ConnectionId, groupName);
        }

        public void JoinPrivateGroup(string partnerUsername)
        {
            var currentUsername = Context.User.Identity.Name;
            using (var db = new ApplicationDbContext())
            {
                var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername && !u.IsDeleted);
                var partnerUser = db.Users.FirstOrDefault(u => u.Username == partnerUsername && !u.IsDeleted);

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
        // VOICE/VIDEO CALLS
        // ========================================================
        public void SendCallOffer(string toUsername, string offerSdp, string callType)
        {
            var fromUsername = Context.User.Identity.Name;
            string toConnectionId;
            if (ConnectedUsers.TryGetValue(toUsername, out toConnectionId))
            {
                Clients.Client(toConnectionId).receiveCallOffer(fromUsername, offerSdp, callType);
                Console.WriteLine($"📞 Call offer: {fromUsername} -> {toUsername} ({callType})");
            }
        }

        public void SendCallAnswer(string toUsername, string answerSdp)
        {
            var fromUsername = Context.User.Identity.Name;
            string toConnectionId;
            if (ConnectedUsers.TryGetValue(toUsername, out toConnectionId))
            {
                Clients.Client(toConnectionId).receiveCallAnswer(fromUsername, answerSdp);
                Console.WriteLine($"📞 Call answered: {fromUsername} -> {toUsername}");
            }
        }

        public void SendIceCandidate(string toUsername, string candidate)
        {
            var fromUsername = Context.User.Identity.Name;
            string toConnectionId;
            if (ConnectedUsers.TryGetValue(toUsername, out toConnectionId))
            {
                Clients.Client(toConnectionId).receiveIceCandidate(fromUsername, candidate);
            }
        }

        public void EndCall(string toUsername)
        {
            var fromUsername = Context.User.Identity.Name;
            string toConnectionId;
            if (ConnectedUsers.TryGetValue(toUsername, out toConnectionId))
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
            using (var db = new ApplicationDbContext())
            {
                var receiver = db.Users.FirstOrDefault(u => u.Username == receiverUsername);
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

            using (var db = new ApplicationDbContext())
            {
                var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername);
                var partner = db.Users.FirstOrDefault(u => u.Username == partnerUsername);

                if (currentUser != null && partner != null)
                {
                    var unreadMessages = db.PrivateMessages
                        .Where(m => m.SenderId == partner.Id
                                 && m.ReceiverId == currentUser.Id
                                 && !m.IsRead)
                        .ToList();

                    foreach (var msg in unreadMessages)
                    {
                        msg.IsRead = true;
                        msg.ReadAt = DateTime.Now;
                        msg.Status = MessageStatus.Read;
                    }

                    db.SaveChanges();

                    if (UserHandler.UsernameToConnectionId.TryGetValue(partnerUsername, out string partnerConnectionId))
                    {
                        Clients.Client(partnerConnectionId).messagesMarkedAsRead(currentUsername);
                        Console.WriteLine($"✅ Notified {partnerUsername} that messages were read");
                    }
                }
            }
        }

        private string GetConnectionIdByUsername(string username)
        {
            string connectionId;
            if (ConnectedUsers.TryGetValue(username, out connectionId))
            {
                return connectionId;
            }
            return null;
        }
        // ========================================================
        // TYPING INDICATOR
        // ========================================================
        public void UserTyping(string partnerUsername)
        {
            try
            {
                var currentUsername = Context.User.Identity.Name;
                using (var db = new ApplicationDbContext())
                {
                    var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername && !u.IsDeleted);
                    var partnerUser = db.Users.FirstOrDefault(u => u.Username == partnerUsername && !u.IsDeleted);

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
                using (var db = new ApplicationDbContext())
                {
                    var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername && !u.IsDeleted);
                    var partnerUser = db.Users.FirstOrDefault(u => u.Username == partnerUsername && !u.IsDeleted);

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
        // DTO CLASS
        // ========================================================
        public class ChatMessageDTO
        {
            public string Type { get; set; }
            public string Content { get; set; }
        }
    }
}