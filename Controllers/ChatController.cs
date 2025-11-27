using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Online_chat.Models;
using Microsoft.AspNet.Identity;
using System.Data.Entity;

namespace Online_chat.Controllers
{
    [Authorize]
    public class ChatController : BaseController
    {

        public class ChatViewModel
        {
            public string CurrentUserAvatarUrl { get; set; }
            public int CurrentUserAvatarVersion { get; set; }
            public string SelectedFriendUsername { get; set; }
            public string BlockStatus { get; set; }
            public string BlockedUserDisplayName { get; set; }
        }

        public ActionResult Index(string friendUsername)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

            var viewModel = new ChatViewModel
            {
                SelectedFriendUsername = friendUsername,
                BlockStatus = "None"
            };

            if (currentUser != null)
            {
                viewModel.CurrentUserAvatarUrl = currentUser.AvatarUrl;
                viewModel.CurrentUserAvatarVersion = (int)currentUser.AvatarVersion;
            }

            if (!string.IsNullOrEmpty(friendUsername) && currentUser != null)
            {
                var friendUser = _context.Users.FirstOrDefault(u => u.Username == friendUsername);
                if (friendUser != null)
                {
                    var youBlockedThem = _context.BlockedUsers.Any(b => b.BlockerId == currentUser.Id && b.BlockedId == friendUser.Id);
                    var theyBlockedYou = _context.BlockedUsers.Any(b => b.BlockerId == friendUser.Id && b.BlockedId == currentUser.Id);

                    if (youBlockedThem)
                    {
                        viewModel.BlockStatus = "YouBlocked";
                        viewModel.BlockedUserDisplayName = friendUser.DisplayName ?? friendUser.Username;
                    }
                    else if (theyBlockedYou)
                    {
                        viewModel.BlockStatus = "TheyBlocked";
                    }
                }
            }

            return View(viewModel);
        }

        [HttpGet]
        public JsonResult GetConversations(string filter)
        {
            try
            {
                var currentUsername = User.Identity.Name;
                var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

                if (currentUser == null) return Json(new List<ConversationViewModel>(), JsonRequestBehavior.AllowGet);

                // ID của User hiện tại là String
                var currentUserId = currentUser.Id;

                var hiddenPartnerUsernames = _context.HiddenConversations
                    .Where(h => h.UserId == currentUserId.ToString())
                    .Select(h => h.PartnerUsername)
                    .ToList();

                // Key của dictionary: ConversationId ép về string để dễ so sánh
                var pinnedConversations = _context.PinnedConversations
                    .Where(p => p.UserId == currentUserId)
                    .ToList() // Lấy về bộ nhớ trước
                    .ToDictionary(p => new { Id = p.ConversationId.ToString(), Type = p.ConversationType }, p => p.PinnedAt);

                var conversations = new List<ConversationViewModel>();

                // --- XỬ LÝ PRIVATE MESSAGES ---
                // Đảm bảo SenderId/ReceiverId trong DB đã là String
                var privateMessages = _context.PrivateMessages
                .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                .ToList()
                .GroupBy(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                    .Select(g => new
                    {
                        PartnerId = g.Key, // Đây là string
                        LastMessage = g.OrderByDescending(m => m.Timestamp).FirstOrDefault()
                    })
                    .ToList();

                foreach (var pm in privateMessages)
                {
                    var partner = _context.Users.Find(pm.PartnerId); // Find hoạt động tốt vì PartnerId là string
                    if (partner != null && partner.Username != currentUsername && !hiddenPartnerUsernames.Contains(partner.Username))
                    {
                        var unreadCount = _context.PrivateMessages.Count(m =>
                            m.SenderId == pm.PartnerId &&
                            m.ReceiverId == currentUserId &&
                            !m.IsRead);

                        var isPinned = pinnedConversations.TryGetValue(
                        new { Id = partner.Id.ToString(), Type = "Private" }, // Convert partner.Id to string
                        out var pinnedAt);

                        conversations.Add(new ConversationViewModel
                        {
                            Type = "Private",
                            Id = partner.Id.ToString(),
                            Username = partner.Username,
                            DisplayName = partner.DisplayName ?? partner.Username,
                            Name = partner.DisplayName ?? partner.Username,
                            AvatarUrl = partner.AvatarUrl ?? "/Content/default-avatar.png",
                            LastMessage = pm.LastMessage?.Content ?? "",
                            LastMessageTimestamp = pm.LastMessage?.Timestamp ?? DateTime.MinValue,
                            UnreadCount = unreadCount,
                            IsPinned = isPinned,
                            PinnedAt = isPinned ? pinnedAt : DateTime.MinValue
                        });
                    }
                }

                // --- XỬ LÝ GROUP MESSAGES ---
                var groupMemberships = _context.GroupMembers
                    .Where(gm => gm.UserId == currentUser.Id)
                    .Include(gm => gm.Group.Members.Select(m => m.User)) // Eager load members and their users
                    .ToList();

                foreach (var membership in groupMemberships)
                {
                    var group = membership.Group;
                    if (group != null)
                    {
                        var lastMessage = _context.GroupMessages
                            .Where(gm => gm.GroupId == group.Id)
                            .OrderByDescending(m => m.Timestamp)
                            .FirstOrDefault();

                        var isPinned = pinnedConversations.TryGetValue(new { Id = group.Id.ToString(), Type = "Group" }, out var pinnedAt);

                        // Get member avatars for composite avatar
                        var allMemberAvatars = group.Members
                            .Select(m => m.User.AvatarUrl ?? "/Content/default-avatar.png")
                            .ToList();

                        List<string> memberAvatars;
                        if (allMemberAvatars.Count > 3)
                        {
                            // Randomly select 3
                            var rnd = new Random();
                            memberAvatars = allMemberAvatars.OrderBy(x => rnd.Next()).Take(3).ToList();
                        }
                        else
                        {
                            memberAvatars = allMemberAvatars;
                        }

                        conversations.Add(new ConversationViewModel
                        {
                            Type = "Group",
                            Id = group.Id.ToString(),
                            Name = group.Name,
                            Username = null,
                            DisplayName = group.Name,
                            AvatarUrl = group.AvatarUrl, // Keep custom uploaded avatar if it exists
                            MemberAvatarUrls = memberAvatars, // Add the list here
                            LastMessage = lastMessage?.Content ?? "",
                            LastMessageTimestamp = lastMessage?.Timestamp ?? DateTime.MinValue,
                            UnreadCount = 0,
                            IsPinned = isPinned,
                            PinnedAt = isPinned ? pinnedAt : DateTime.MinValue
                        });
                    }
                }

                // Lọc dựa trên tham số
                if (filter == "unread")
                {
                    conversations = conversations.Where(c => c.UnreadCount > 0).ToList();
                }
                else if (filter == "groups")
                {
                    conversations = conversations.Where(c => c.Type == "Group").ToList();
                }

                // Sắp xếp: ghim lên đầu, sau đó theo thời gian ghim, cuối cùng là tin nhắn mới nhất
                var sortedConversations = conversations
                    .OrderByDescending(c => c.IsPinned)
                    .ThenByDescending(c => c.PinnedAt)
                    .ThenByDescending(c => c.LastMessageTimestamp)
                    .ToList();

                return Json(sortedConversations, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"Error in GetConversations: {ex.Message}");
                return Json(new List<ConversationViewModel>(), JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public ActionResult GetChatUsers()
        {
            try
            {
                var username = User.Identity.Name;
                var currentUser = _context.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);

                if (currentUser == null)
                {
                    return Json(new List<object>(), JsonRequestBehavior.AllowGet);
                }

                var blockedUserIds = _context.BlockedUsers
                    .Where(b => b.BlockerId == currentUser.Id || b.BlockedId == currentUser.Id)
                    .Select(b => b.BlockerId == currentUser.Id ? b.BlockedId : b.BlockerId)
                    .ToHashSet();

                var users = _context.Users
                    .Where(u => !u.IsDeleted && u.Id != currentUser.Id && !blockedUserIds.Contains(u.Id))
                    .OrderBy(u => u.Username)
                    .Select(u => new
                    {
                        Id = u.Id,
                        Username = u.Username,
                        AvatarUrl = u.AvatarUrl
                    })
                    .ToList();

                return Json(users, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult DeleteConversation(string partnerUsername)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);
            var partner = _context.Users.FirstOrDefault(u => u.Username == partnerUsername);

            if (currentUser == null || partner == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            // Check if it's already hidden
            var existingHiddenConversation = _context.HiddenConversations
                .FirstOrDefault(h => h.UserId == currentUser.Id.ToString() && h.PartnerUsername == partnerUsername);

            if (existingHiddenConversation == null)
            {
                var hiddenConversation = new HiddenConversation
                {
                    UserId = currentUser.Id.ToString(),
                    PartnerUsername = partnerUsername
                };
                _context.HiddenConversations.Add(hiddenConversation);
                _context.SaveChanges();
            }

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult PinConversation(string conversationId, string conversationType)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (currentUser == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            var existingPin = _context.PinnedConversations.FirstOrDefault(p =>
                p.UserId == currentUser.Id &&
                p.ConversationId == conversationId && 
                p.ConversationType == conversationType);

            bool isPinned;

            if (existingPin != null)
            {
                _context.PinnedConversations.Remove(existingPin);
                isPinned = false;
            }
            else
            {
                var newPin = new PinnedConversation
                {
                    UserId = currentUser.Id,
                    ConversationId = conversationId, 
                    ConversationType = conversationType,
                    PinnedAt = DateTime.UtcNow
                };
                _context.PinnedConversations.Add(newPin);
                isPinned = true;
            }

            _context.SaveChanges();
            return Json(new { success = true, isPinned = isPinned });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ReportConversation(string reportedUsername, string reason)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);
            var reportedUser = _context.Users.FirstOrDefault(u => u.Username == reportedUsername);

            if (currentUser == null || reportedUser == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            if (string.IsNullOrWhiteSpace(reason))
            {
                return Json(new { success = false, message = "Reason is required." });
            }

            var report = new Report
            {
                ReporterId = currentUser.Id, 
                ReportedUserId = reportedUser.Id, 
                Reason = reason,
                CreatedAt = DateTime.UtcNow
            };

            _context.Reports.Add(report);
            _context.SaveChanges();

            return Json(new { success = true });
        }

        [HttpGet]
        public JsonResult GetUnreadMessageCounts()
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (currentUser == null)
            {
                return Json(new { }, JsonRequestBehavior.AllowGet);
            }

            // Đếm số tin nhắn chưa đọc từ mỗi người
            var unreadCounts = _context.PrivateMessages
                .Where(m => m.ReceiverId == currentUser.Id && !m.IsRead)
                .Include(m => m.Sender)
                .ToList()
                .GroupBy(m => m.Sender.Username)
                .Select(g => new { Username = g.Key, Count = g.Count() })
                .ToDictionary(x => x.Username, x => x.Count);

            return Json(unreadCounts, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult MarkAsRead(string partnerUsername)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);
            var partner = _context.Users.FirstOrDefault(u => u.Username == partnerUsername);

            if (currentUser == null || partner == null)
            {
                return Json(new { success = false });
            }

            var unreadMessages = _context.PrivateMessages
                .Where(m => m.SenderId == partner.Id
                         && m.ReceiverId == currentUser.Id
                         && !m.IsRead)
                .ToList();

            foreach (var msg in unreadMessages)
            {
                msg.IsRead = true;
                msg.ReadAt = DateTime.UtcNow;
                msg.Status = MessageStatus.Read;
            }

            _context.SaveChanges();
            return Json(new { success = true, count = unreadMessages.Count });
        }

        [HttpGet]
        public JsonResult GetChatHistory(string partnerUsername)
        {
            var currentUsername = User.Identity.Name;

            using (var db = new ApplicationDbContext())
            {
                var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername);
                var partner = db.Users.FirstOrDefault(u => u.Username == partnerUsername);

                if (currentUser == null || partner == null)
                {
                    return Json(new { success = false, message = "User not found" }, JsonRequestBehavior.AllowGet);
                }

                var messages = db.PrivateMessages
                    .Include(m => m.Sender)
                    .Include(m => m.Receiver)
                    .Include(m => m.ParentMessage)
                    .Include(m => m.ParentMessage.Sender)
                    .Include(m => m.ForwardedFrom)
                    .Include(m => m.Reactions.Select(r => r.User))  // ← THÊM DÒNG NÀY
                    .Where(m =>
                ((m.SenderId == currentUser.Id && m.ReceiverId == partner.Id) ||
                 (m.SenderId == partner.Id && m.ReceiverId == currentUser.Id))
                && !m.IsDeleted) 
            .OrderBy(m => m.Timestamp)
                    .ToList();

                var result = messages.Select(m => new
                {
                    Id = m.Id,
                    Content = m.Content,
                    SenderId = m.Sender.Id,
                    SenderUsername = m.Sender.Username,
                    SenderAvatar = m.Sender.AvatarUrl ?? "/Content/default-avatar.png",
                    Timestamp = m.Timestamp,
                    Status = m.Status.ToString(),
                    IsDeleted = m.IsDeleted,
                    EditedAt = m.EditedAt,
                    ParentMessage = m.ParentMessage != null ? new
                    {
                        Content = m.ParentMessage.Content,
                        SenderUsername = m.ParentMessage.Sender.Username
                    } : null,
                    ForwardedFrom = m.ForwardedFrom != null ? new
                    {
                        Username = m.ForwardedFrom.Username,
                        DisplayName = m.ForwardedFrom.DisplayName
                    } : null,
                    Reactions = m.Reactions.Select(r => new
                    {
                        UserId = r.UserId,
                        Username = r.User.Username,
                        Emoji = r.Emoji
                    }).ToList()
                }).ToList();

                return Json(new { success = true, messages = result }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetGroupChatHistory(int groupId)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (currentUser == null)
            {
                return Json(new { success = false, message = "User not found" }, JsonRequestBehavior.AllowGet);
            }

            // Check if user is a member of the group
            var isMember = _context.GroupMembers.Any(gm => gm.GroupId == groupId && gm.UserId == currentUser.Id);
            if (!isMember)
            {
                // Or handle as an error, e.g., return new HttpStatusCodeResult(HttpStatusCode.Forbidden);
                return Json(new { success = false, message = "Access denied." }, JsonRequestBehavior.AllowGet);
            }

            var messages = _context.GroupMessages
                .Include(m => m.Sender)
                .Where(m => m.GroupId == groupId)
                .OrderBy(m => m.Timestamp)
                .ToList();

            var result = messages.Select(m => new
            {
                Id = m.Id,
                SenderUsername = m.Sender.Username,
                SenderAvatar = m.Sender.AvatarUrl ?? "/Content/default-avatar.png",
                Content = m.Content,
                Timestamp = m.Timestamp.ToString("o"),
                // Add any other properties you need, similar to GetChatHistory
                IsDeleted = m.IsDeleted,
                EditedAt = m.EditedAt?.ToString("o")
            }).ToList();

            return Json(new { success = true, messages = result }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ClearHistory(string partnerUsername)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);
            var partnerUser = _context.Users.FirstOrDefault(u => u.Username == partnerUsername);

            if (currentUser == null || partnerUser == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            var messagesToDelete = _context.PrivateMessages
                .Where(m => (m.SenderId == currentUser.Id && m.ReceiverId == partnerUser.Id) ||
                            (m.SenderId == partnerUser.Id && m.ReceiverId == currentUser.Id))
                .ToList();

            foreach (var msg in messagesToDelete)
            {
                msg.IsDeleted = true;
            }

            _context.SaveChanges();

            return Json(new { success = true });
        }

        [HttpGet]
        public JsonResult GetConversationInfo(string partnerUsername)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);
            var partnerUser = _context.Users.FirstOrDefault(u => u.Username == partnerUsername);

            if (currentUser == null || partnerUser == null)
            {
                return Json(new { success = false, message = "User not found" }, JsonRequestBehavior.AllowGet);
            }

            var messages = _context.PrivateMessages
                .Where(m => (m.SenderId == currentUser.Id && m.ReceiverId == partnerUser.Id) ||
                             (m.SenderId == partnerUser.Id && m.ReceiverId == currentUser.Id))
                .OrderByDescending(m => m.Timestamp)
                .ToList();

            // Parse JSON content để lấy type
            var images = messages
                .Select(m => new
                {
                    Message = m,
                    ParsedContent = ParseMessageContent(m.Content)
                })
                .Where(x => x.ParsedContent != null && x.ParsedContent.Type == "image")
                .Select(x => new
                {
                    Url = x.ParsedContent.Content,
                    Timestamp = x.Message.Timestamp.ToString("dd/MM/yyyy")
                })
                .Take(20)
                .ToList();

            var videos = messages
                .Select(m => new
                {
                    Message = m,
                    ParsedContent = ParseMessageContent(m.Content)
                })
                .Where(x => x.ParsedContent != null && x.ParsedContent.Type == "video")
                .Select(x => new
                {
                    Url = x.ParsedContent.Content,
                    Timestamp = x.Message.Timestamp.ToString("dd/MM/yyyy")
                })
                .Take(20)
                .ToList();

            var files = messages
                .Select(m => new
                {
                    Message = m,
                    ParsedContent = ParseMessageContent(m.Content)
                })
                .Where(x => x.ParsedContent != null && x.ParsedContent.Type == "file")
                .Select(x => new
                {
                    Url = x.ParsedContent.Content,
                    FileName = x.ParsedContent.FileName ?? "File",
                    FileSize = x.ParsedContent.FileSize ?? "N/A",
                    Timestamp = x.Message.Timestamp.ToString("dd/MM/yyyy")
                })
                .Take(20)
                .ToList();

            // Gộp ảnh và video vào một danh sách
            var allMedia = images.Cast<object>().Concat(videos.Cast<object>()).ToList();

            return Json(new { success = true, images = allMedia, files = files }, JsonRequestBehavior.AllowGet);
        }

        private class MessageContentModel
        {
            public string Type { get; set; }
            public string Content { get; set; }
            public string FileName { get; set; }
            public string FileSize { get; set; }
        }

        private MessageContentModel ParseMessageContent(string jsonContent)
        {
            try
            {
                return Newtonsoft.Json.JsonConvert.DeserializeObject<MessageContentModel>(jsonContent);
            }
            catch
            {
                return null;
            }
        }

        // Helper methods pour extraire les données JSON
        private string GetContentFromJson(string jsonContent)
        {
            try
            {
                var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonContent);
                return obj?.content?.ToString() ?? jsonContent;
            }
            catch
            {
                return jsonContent;
            }
        }

        private string GetFileNameFromJson(string jsonContent)
        {
            try
            {
                var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(jsonContent);
                return obj?.fileName?.ToString() ?? "File";
            }
            catch
            {
                return "File";
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult CreateGroup(string groupName, List<string> memberUsernames)
        {
            if (string.IsNullOrWhiteSpace(groupName))
            {
                return Json(new { success = false, message = "Tên nhóm không được để trống." });
            }

            if (memberUsernames == null || memberUsernames.Count < 1)
            {
                return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 thành viên để tạo nhóm." });
            }

            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

            // Create the group
            var newGroup = new Group
            {
                Name = groupName,
                CreatedBy = currentUsername,
                CreatedAt = DateTime.UtcNow,
                CreatedById = currentUser.Id
            };
            _context.Groups.Add(newGroup);
            _context.SaveChanges(); // Save to get the newGroup.Id

            // Add the creator to the group
            var creatorMember = new GroupMember
            {
                GroupId = newGroup.Id,
                UserId = currentUser.Id,
                JoinedAt = DateTime.UtcNow
            };
            _context.GroupMembers.Add(creatorMember);

            // Add the selected members
            foreach (var username in memberUsernames)
            {
                var user = _context.Users.FirstOrDefault(u => u.Username == username);
                if (user != null)
                {
                    var member = new GroupMember
                    {
                        GroupId = newGroup.Id,
                        UserId = user.Id,
                        JoinedAt = DateTime.UtcNow
                    };
                    _context.GroupMembers.Add(member);
                }
            }

            _context.SaveChanges();

            return Json(new { success = true, groupId = newGroup.Id, groupName = newGroup.Name });
        }

        [HttpGet]
        public JsonResult SearchMessages(string term, string partnerUsername)
        {
            if (string.IsNullOrWhiteSpace(term) || string.IsNullOrWhiteSpace(partnerUsername))
            {
                return Json(new { success = false, message = "Invalid parameters." }, JsonRequestBehavior.AllowGet);
            }

            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);
            var partnerUser = _context.Users.FirstOrDefault(u => u.Username == partnerUsername);

            if (currentUser == null || partnerUser == null)
            {
                return Json(new { success = false, message = "User not found." }, JsonRequestBehavior.AllowGet);
            }

            var messages = _context.PrivateMessages
                .Include(m => m.Sender)
                .Where(m => ((m.SenderId == currentUser.Id && m.ReceiverId == partnerUser.Id) ||
                             (m.SenderId == partnerUser.Id && m.ReceiverId == currentUser.Id)) &&
                            !m.IsDeleted &&
                            m.MessageType == "text" && // Chỉ tìm kiếm trong tin nhắn văn bản
                            m.Content.Contains(term))
                .OrderByDescending(m => m.Timestamp)
                .Take(50) // Limit results
                .ToList();

            var results = messages.Select(m => new
            {
                Id = m.Id,
                Content = m.Content, // In a real app, you'd want to sanitize and highlight the term
                SenderUsername = m.Sender.Username,
                SenderAvatar = m.Sender.AvatarUrl ?? "/Content/default-avatar.png",
                Timestamp = m.Timestamp
            }).ToList();

            return Json(new { success = true, results = results }, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }

        [HttpGet]
        public JsonResult GetBlockStatus(string partnerUsername)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);
            var partnerUser = _context.Users.FirstOrDefault(u => u.Username == partnerUsername);

            if (currentUser == null || partnerUser == null)
            {
                return Json(new { status = "None" }, JsonRequestBehavior.AllowGet);
            }

            var youBlockedThem = _context.BlockedUsers.Any(b => b.BlockerId == currentUser.Id && b.BlockedId == partnerUser.Id);
            if (youBlockedThem)
            {
                return Json(new { status = "YouBlocked", blockedUserDisplayName = partnerUser.DisplayName ?? partnerUser.Username }, JsonRequestBehavior.AllowGet);
            }

            var theyBlockedYou = _context.BlockedUsers.Any(b => b.BlockerId == partnerUser.Id && b.BlockedId == currentUser.Id);
            if (theyBlockedYou)
            {
                return Json(new { status = "TheyBlocked" }, JsonRequestBehavior.AllowGet);
            }

            return Json(new { status = "None" }, JsonRequestBehavior.AllowGet);
        }
    }
}