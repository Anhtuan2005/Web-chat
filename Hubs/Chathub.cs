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
        private readonly ApplicationDbContext _context = new ApplicationDbContext();

        private static readonly ConcurrentDictionary<string, string> ConnectedUsers = new ConcurrentDictionary<string, string>();
        private static readonly ConcurrentDictionary<string, DateTime> UserLastSeen = new ConcurrentDictionary<string, DateTime>();
        private static readonly ConcurrentDictionary<string, List<object>> _aiConversations = new ConcurrentDictionary<string, List<object>>();

        // ========================================================
        // QUẢN LÝ KẾT NỐI
        // ========================================================

        public override Task OnConnected()
        {
            var username = Context.User.Identity.Name;
            if (!string.IsNullOrEmpty(username))
            {
                ConnectedUsers[username] = Context.ConnectionId;
                UserLastSeen.TryRemove(username, out _);
                Clients.All.userConnected(username);
                var onlineUsers = ConnectedUsers.Keys.ToList();
                Clients.Caller.updateOnlineUsers(onlineUsers);
                Console.WriteLine($"✅ {username} connected - ConnectionId: {Context.ConnectionId}");
            }
            return base.OnConnected();
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            var username = Context.User.Identity.Name;
            if (!string.IsNullOrEmpty(username))
            {
                string connectionId;
                ConnectedUsers.TryRemove(username, out connectionId);
                UserLastSeen[username] = DateTime.UtcNow;
                Clients.All.userDisconnected(username);
                Console.WriteLine($"❌ {username} disconnected");
            }
            return base.OnDisconnected(stopCalled);
        }

        public override Task OnReconnected()
        {
            var username = Context.User.Identity.Name;
            if (!string.IsNullOrEmpty(username))
            {
                ConnectedUsers[username] = Context.ConnectionId;
                UserLastSeen.TryRemove(username, out _);
                Clients.All.userConnected(username);
                Console.WriteLine($"🔄 {username} reconnected");
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

            var sender = _context.Users.FirstOrDefault(u => u.Username == username);
            if (sender == null) return;

            var senderAvatar = sender.AvatarUrl ?? "/Content/default-avatar.png";

            var groupMessage = new GroupMessage
            {
                GroupId = groupId,
                SenderId = sender.Id,
                Content = messageJson,
                Timestamp = timestamp
            };
            _context.GroupMessages.Add(groupMessage);
            _context.SaveChanges();

            var groupName = $"group_{groupId}";
            Clients.Group(groupName).receiveGroupMessage(groupId, username, senderAvatar, messageJson, timestamp.ToString("o"));

            Console.WriteLine($"👥💬 Group {groupId} - {username}: {messageJson}");
        }


        // ========================================================
        // GỬI TIN NHẮN PRIVATE
        // ========================================================
        public void SendPrivateMessage(string partnerUsername, string rawMessage)
        {
            var senderUsername = Context.User.Identity.Name;
            var senderUser = _context.Users.FirstOrDefault(u => u.Username == senderUsername && !u.IsDeleted);
            var partnerUser = _context.Users.FirstOrDefault(u => u.Username == partnerUsername && !u.IsDeleted);

            if (senderUser == null || partnerUser == null) return;

            var timestamp = DateTime.UtcNow;
            var msg = new PrivateMessage
            {
                SenderId = senderUser.Id,
                ReceiverId = partnerUser.Id,
                Content = rawMessage,
                Timestamp = timestamp
            };
            _context.PrivateMessages.Add(msg);
            _context.SaveChanges();

            var groupName = GetPrivateGroupName(senderUser.Id, partnerUser.Id);
            Clients.Group(groupName).receiveMessage(
                senderUser.Username,
                senderUser.AvatarUrl,
                rawMessage,
                timestamp.ToString("o")
            );
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
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername && !u.IsDeleted);
            var partnerUser = _context.Users.FirstOrDefault(u => u.Username == partnerUsername && !u.IsDeleted);

            if (currentUser != null && partnerUser != null)
            {
                var groupName = GetPrivateGroupName(currentUser.Id, partnerUser.Id);
                Groups.Add(Context.ConnectionId, groupName);
                Console.WriteLine($"👥 {currentUsername} joined private group: {groupName}");
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
            var receiver = _context.Users.FirstOrDefault(u => u.Username == receiverUsername);
            if (receiver == null) return;
            Clients.User(receiverUsername).receiveFriendRequest();
        }

        // ========================================================
        // TYPING INDICATOR
        // ========================================================
        public void UserTyping(string partnerUsername)
        {
            try
            {
                var currentUsername = Context.User.Identity.Name;
                Console.WriteLine($"[DEBUG] UserTyping called: {currentUsername} -> {partnerUsername}");

                var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername && !u.IsDeleted);
                var partnerUser = _context.Users.FirstOrDefault(u => u.Username == partnerUsername && !u.IsDeleted);

                if (currentUser == null || partnerUser == null)
                {
                    Console.WriteLine($"[ERROR] User not found: current={currentUser?.Id}, partner={partnerUser?.Id}");
                    return;
                }

                var groupName = GetPrivateGroupName(currentUser.Id, partnerUser.Id);
                Console.WriteLine($"[DEBUG] Sending typing to group: {groupName}");

                Clients.Group(groupName).userTyping(currentUsername);

                Console.WriteLine($"⌨️ Typing: {currentUsername} -> {partnerUsername}");
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
                var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername && !u.IsDeleted);
                var partnerUser = _context.Users.FirstOrDefault(u => u.Username == partnerUsername && !u.IsDeleted);

                if (currentUser == null || partnerUser == null) return;

                var groupName = GetPrivateGroupName(currentUser.Id, partnerUser.Id);
                Clients.Group(groupName).userStoppedTyping(currentUsername);

                Console.WriteLine($"⏹ Stopped typing: {currentUsername} -> {partnerUsername}");
            }
            catch (Exception ex)
            {
                Console.WriteLine($"[ERROR] UserStoppedTyping exception: {ex.Message}");
            }
        }

        // ========================================================
        // CLEANUP
        // ========================================================

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
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