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

namespace Online_chat.Hubs
{
    [Authorize]
    public class ChatHub : Hub
    {
        private readonly ApplicationDbContext _context = new ApplicationDbContext();

        private static readonly Dictionary<string, List<object>> _aiConversations = new Dictionary<string, List<object>>();

        public void SendMessage(string groupName, string messageContent)
        {
            var senderUsername = Context.User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == senderUsername);

            if (user == null || string.IsNullOrWhiteSpace(messageContent)) return;

            Clients.Group(groupName).receiveMessage(
                user.Username,
                user.AvatarUrl,
                messageContent,
                DateTime.UtcNow
            );

            var group = _context.Groups.FirstOrDefault(g => g.GroupName == groupName);
            if (group != null)
            {
                var message = new Message
                {
                    Content = messageContent,
                    Timestamp = DateTime.UtcNow,
                    SenderId = user.Id,
                    GroupId = group.Id
                };
                _context.Messages.Add(message);
                _context.SaveChanges();
            }
        }

        // ✅ FIX: Gửi đúng thứ tự tham số
        public void SendPrivateMessage(string partnerUsername, string rawMessage)
        {
            var senderUsername = Context.User.Identity.Name;
            var senderUser = _context.Users.FirstOrDefault(u => u.Username == senderUsername && !u.IsDeleted);
            var partnerUser = _context.Users.FirstOrDefault(u => u.Username == partnerUsername && !u.IsDeleted);
            if (senderUser == null || partnerUser == null) return;

            // Parse JSON message
            ChatMessageDTO msgObj;
            try
            {
                msgObj = JsonConvert.DeserializeObject<ChatMessageDTO>(rawMessage);
            }
            catch
            {
                msgObj = new ChatMessageDTO { Type = "text", Content = rawMessage };
            }

            // Lưu vào database
            var msg = new PrivateMessage
            {
                SenderId = senderUser.Id,
                ReceiverId = partnerUser.Id,
                Content = rawMessage,  // ✅ Lưu RAW JSON để parse lại khi load history
                MessageType = msgObj.Type,
                Timestamp = DateTime.UtcNow
            };
            _context.PrivateMessages.Add(msg);
            _context.SaveChanges();

            // ✅ FIX: Gửi đúng 4 tham số theo thứ tự: username, avatar, message (JSON), timestamp
            var groupName = GetPrivateGroupName(senderUser.Id, partnerUser.Id);
            Clients.Group(groupName).receiveMessage(
                senderUser.Username,
                senderUser.AvatarUrl,
                rawMessage,              // ✅ Gửi JSON nguyên bản
                DateTime.UtcNow          // ✅ Gửi DateTime object (không ToString)
            );
        }

        // ✅ FIX: Đổi tên hàm từ ReceiveMessage -> receiveMessage
        public async Task SendMessageToAI(string messageContent)
        {
            var senderUsername = Context.User.Identity.Name;
            if (string.IsNullOrWhiteSpace(messageContent)) return;

            // Khởi tạo lịch sử hội thoại
            if (!_aiConversations.ContainsKey(senderUsername))
            {
                _aiConversations[senderUsername] = new List<object>
                {
                    new { role = "system", content = "Bạn là một trợ lý ảo thân thiện, luôn trả lời bằng cùng ngôn ngữ mà người dùng đang sử dụng." }
                };
            }
            _aiConversations[senderUsername].Add(new { role = "user", content = messageContent });

            string apiKey = ConfigurationManager.AppSettings["OpenAIApiKey"];
            if (string.IsNullOrEmpty(apiKey))
            {
                // ✅ FIX: Đổi từ ReceiveMessage -> receiveMessage
                Clients.Caller.receiveMessage(
                    "AI Assistant",
                    null,
                    JsonConvert.SerializeObject(new { type = "text", content = "Xin lỗi, API AI chưa được cấu hình." }),
                    DateTime.UtcNow
                );
                return;
            }

            string endpoint = "https://api.openai.com/v1/chat/completions";
            var requestBody = new
            {
                model = "gpt-3.5-turbo",
                messages = _aiConversations[senderUsername],
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

                        _aiConversations[senderUsername].Add(new { role = "assistant", content = aiReply });

                        // ✅ FIX: Gửi đúng format JSON
                        Clients.Caller.receiveMessage(
                            "AI Assistant",
                            null,
                            JsonConvert.SerializeObject(new { type = "text", content = aiReply }),
                            DateTime.UtcNow
                        );

                        Console.WriteLine($"✅ AI replied to {senderUsername}: {aiReply.Substring(0, Math.Min(50, aiReply.Length))}...");
                    }
                    else
                    {
                        var errorMsg = await response.Content.ReadAsStringAsync();
                        Console.WriteLine($"❌ AI API Error: {response.StatusCode} - {errorMsg}");

                        Clients.Caller.receiveMessage(
                            "AI Assistant",
                            null,
                            JsonConvert.SerializeObject(new { type = "text", content = "Xin lỗi, tôi đang gặp sự cố. Vui lòng thử lại sau." }),
                            DateTime.UtcNow
                        );
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"❌ AI Exception: {ex.Message}");
                    Clients.Caller.receiveMessage(
                        "AI Assistant",
                        null,
                        JsonConvert.SerializeObject(new { type = "text", content = $"Lỗi: {ex.Message}" }),
                        DateTime.UtcNow
                    );
                }
            }
        }

        public override Task OnDisconnected(bool stopCalled)
        {
            var username = Context.User.Identity.Name;
            if (_aiConversations.ContainsKey(username))
                _aiConversations.Remove(username);

            return base.OnDisconnected(stopCalled);
        }

        public void SendFriendRequestNotification(string receiverUsername)
        {
            var receiver = _context.Users.FirstOrDefault(u => u.Username == receiverUsername);
            if (receiver == null) return;

            Clients.User(receiverUsername).receiveFriendRequest();
        }

        public void JoinPrivateGroup(string partnerUsername)
        {
            var currentUsername = Context.User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);
            var partnerUser = _context.Users.FirstOrDefault(u => u.Username == partnerUsername);

            if (currentUser != null && partnerUser != null)
            {
                var groupName = GetPrivateGroupName(currentUser.Id, partnerUser.Id);
                Groups.Add(Context.ConnectionId, groupName);
            }
        }

        private string GetPrivateGroupName(int userId1, int userId2)
        {
            return userId1 < userId2 ? $"private_{userId1}_{userId2}" : $"private_{userId2}_{userId1}";
        }

        public async Task JoinGroup(string groupName)
        {
            await Groups.Add(Context.ConnectionId, groupName);
        }

        public override Task OnConnected()
        {
            return base.OnConnected();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }

        public class ChatMessageDTO
        {
            public string Type { get; set; }
            public string Content { get; set; }
        }
    }
}