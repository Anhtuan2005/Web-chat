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

            Clients.Group(groupName).ReceiveMessage(
                user.Username,
                user.AvatarUrl,
                messageContent,
                DateTime.Now.ToString("HH:mm")
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

        public void SendPrivateMessage(string partnerUsername, string rawMessage)
        {
            var senderUsername = Context.User.Identity.Name;
            var senderUser = _context.Users.FirstOrDefault(u => u.Username == senderUsername && !u.IsDeleted);
            var partnerUser = _context.Users.FirstOrDefault(u => u.Username == partnerUsername && !u.IsDeleted);
            if (senderUser == null || partnerUser == null) return;

            ChatMessageDTO msgObj;
            try { msgObj = JsonConvert.DeserializeObject<ChatMessageDTO>(rawMessage); }
            catch { msgObj = new ChatMessageDTO { Type = "text", Content = rawMessage }; }

            var msg = new PrivateMessage
            {
                SenderId = senderUser.Id,
                ReceiverId = partnerUser.Id,
                Content = msgObj.Content,
                MessageType = msgObj.Type,
                Timestamp = DateTime.UtcNow
            };
            _context.PrivateMessages.Add(msg);
            _context.SaveChanges();

            var groupName = GetPrivateGroupName(senderUser.Id, partnerUser.Id);
            Clients.Group(groupName).ReceiveMessage(senderUser.Username, senderUser.AvatarUrl, msgObj.Type, msgObj.Content, DateTime.Now.ToString("HH:mm"));
        }

        public async Task SendMessageToAI(string messageContent)
        {
            var senderUsername = Context.User.Identity.Name;
            if (string.IsNullOrWhiteSpace(messageContent)) return; 

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
                Clients.Caller.ReceiveMessage("AI Assistant", null, "text", "Xin lỗi, API AI chưa được cấu hình. Hãy liên hệ admin.", DateTime.Now.ToString("HH:mm"));
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
                        string aiReply = result.choices[0].message.content.ToString();  // .ToString() để safe
                        _aiConversations[senderUsername].Add(new { role = "assistant", content = aiReply });

                        Clients.Caller.ReceiveMessage("AI Assistant", null, "text", aiReply, DateTime.Now.ToString("HH:mm"));
                        Console.WriteLine($"AI reply to {senderUsername}: {aiReply.Substring(0, Math.Min(50, aiReply.Length))}...");  // Log debug
                    }
                    else
                    {
                        var errorMsg = $"Lỗi API: {response.StatusCode} - {await response.Content.ReadAsStringAsync()}";
                        Console.WriteLine(errorMsg);  // Log error
                        Clients.Caller.ReceiveMessage("AI Assistant", null, "text", "Xin lỗi, tôi đang gặp sự cố. Vui lòng thử lại sau.", DateTime.Now.ToString("HH:mm"));
                    }
                }
                catch (Exception ex)
                {
                    Console.WriteLine($"Exception in AI: {ex.Message}");  // Log full error
                    Clients.Caller.ReceiveMessage("AI Assistant", null, "text", $"Lỗi: {ex.Message}", DateTime.Now.ToString("HH:mm"));
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
            public string Type { get; set; }  // "text", "image", "video", "file"
            public string Content { get; set; }
        }

    }
}