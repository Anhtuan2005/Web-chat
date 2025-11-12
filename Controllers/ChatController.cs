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
        private ApplicationDbContext db = new ApplicationDbContext();

        public ActionResult Index(string friend)
        {
            ViewBag.SelectedFriendUsername = friend;
            return View();
        }

        [HttpGet]
        public JsonResult GetConversations(string filter)
        {
            try
            {
                var currentUsername = User.Identity.Name;
                var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername);

                if (currentUser == null)
                {
                    return Json(new List<ConversationViewModel>(), JsonRequestBehavior.AllowGet);
                }

                var currentUserId = currentUser.Id;
                var conversations = new List<ConversationViewModel>();

                // Lấy các cuộc trò chuyện cá nhân
                var privateMessages = db.PrivateMessages
                    .Where(m => m.SenderId == currentUserId || m.ReceiverId == currentUserId)
                    .ToList() // Execute query first
                    .GroupBy(m => m.SenderId == currentUserId ? m.ReceiverId : m.SenderId)
                    .Select(g => new
                    {
                        PartnerId = g.Key,
                        LastMessage = g.OrderByDescending(m => m.Timestamp).FirstOrDefault()
                    })
                    .ToList();

                foreach (var pm in privateMessages)
                {
                    var partner = db.Users.Find(pm.PartnerId);
                    if (partner != null && partner.Username != currentUsername)
                    {
                        var unreadCount = db.PrivateMessages.Count(m =>
                            m.SenderId == pm.PartnerId &&
                            m.ReceiverId == currentUserId &&
                            !m.IsRead);

                        conversations.Add(new ConversationViewModel
                        {
                            Type = "Private",
                            Id = partner.Id,
                            Username = partner.Username,
                            DisplayName = partner.DisplayName ?? partner.Username,
                            Name = partner.DisplayName ?? partner.Username,
                            AvatarUrl = partner.AvatarUrl ?? "/Content/default-avatar.png",
                            LastMessage = pm.LastMessage?.Content ?? "",
                            LastMessageTimestamp = pm.LastMessage?.Timestamp ?? DateTime.MinValue,
                            UnreadCount = unreadCount
                        });
                    }
                }

                // Lấy các cuộc trò chuyện nhóm
                var groupMemberships = db.GroupMembers
                    .Where(gm => gm.UserId == currentUserId)
                    .Include(gm => gm.Group)
                    .ToList();

                foreach (var membership in groupMemberships)
                {
                    var group = membership.Group;
                    if (group != null)
                    {
                        var lastMessage = db.GroupMessages
                            .Where(gm => gm.GroupId == group.Id)
                            .OrderByDescending(m => m.Timestamp)
                            .FirstOrDefault();

                        conversations.Add(new ConversationViewModel
                        {
                            Type = "Group",
                            Id = group.Id,
                            Name = group.Name,
                            Username = null,
                            DisplayName = group.Name,
                            AvatarUrl = group.AvatarUrl ?? "/Content/default-group-avatar.png",
                            LastMessage = lastMessage?.Content ?? "",
                            LastMessageTimestamp = lastMessage?.Timestamp ?? DateTime.MinValue,
                            UnreadCount = 0
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

                // Sắp xếp theo thời gian tin nhắn cuối
                var sortedConversations = conversations
                    .OrderByDescending(c => c.LastMessageTimestamp)
                    .ToList();

                return Json(sortedConversations, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                // Log error (you should add logging here)
                System.Diagnostics.Debug.WriteLine($"Error in GetConversations: {ex.Message}");
                return Json(new List<ConversationViewModel>(), JsonRequestBehavior.AllowGet);
            }
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
                .GroupBy(m => m.Sender.Username)
                .Select(g => new { Username = g.Key, Count = g.Count() })
                .ToDictionary(x => x.Username, x => x.Count);

            return Json(unreadCounts, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
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
            var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername);
            var partnerUser = db.Users.FirstOrDefault(u => u.Username == partnerUsername);

            if (currentUser == null || partnerUser == null)
            {
                return Json(new { success = false, message = "User not found" }, JsonRequestBehavior.AllowGet);
            }

            var messages = db.PrivateMessages
                .Where(m => (m.SenderId == currentUser.Id && m.ReceiverId == partnerUser.Id) ||
                             (m.SenderId == partnerUser.Id && m.ReceiverId == currentUser.Id))
                .OrderBy(m => m.Timestamp)
                .Select(m => new
                {
                    Id = m.Id,
                    SenderUsername = m.Sender.Username,
                    SenderAvatar = m.Sender.AvatarUrl,
                    Content = m.Content,
                    Timestamp = m.Timestamp,
                    Status = m.Status
                })
                .ToList();

            return Json(new { success = true, messages = messages }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ClearHistory(string partnerUsername)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername);
            var partnerUser = db.Users.FirstOrDefault(u => u.Username == partnerUsername);

            if (currentUser == null || partnerUser == null)
            {
                return Json(new { success = false, message = "User not found" });
            }

            var messagesToDelete = db.PrivateMessages
                .Where(m => (m.SenderId == currentUser.Id && m.ReceiverId == partnerUser.Id) ||
                             (m.SenderId == partnerUser.Id && m.ReceiverId == currentUser.Id));

            db.PrivateMessages.RemoveRange(messagesToDelete);
            db.SaveChanges();

            return Json(new { success = true });
        }

        [HttpGet]
        public JsonResult GetConversationInfo(string partnerUsername)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername);
            var partnerUser = db.Users.FirstOrDefault(u => u.Username == partnerUsername);

            if (currentUser == null || partnerUser == null)
            {
                return Json(new { success = false, message = "User not found" }, JsonRequestBehavior.AllowGet);
            }

            var messages = db.PrivateMessages
                .Where(m => (m.SenderId == currentUser.Id && m.ReceiverId == partnerUser.Id) ||
                             (m.SenderId == partnerUser.Id && m.ReceiverId == currentUser.Id))
                .OrderByDescending(m => m.Timestamp)
                .ToList();

            var images = messages
                .Where(m => m.MessageType == "image")
                .Select(m => new { Url = m.Content, Timestamp = m.Timestamp })
                .Take(20)
                .ToList();

            var files = messages
                .Where(m => m.MessageType == "file")
                .Select(m => new { Url = m.Content, FileName = "File", Timestamp = m.Timestamp })
                .Take(20)
                .ToList();

            return Json(new { success = true, images = images, files = files }, JsonRequestBehavior.AllowGet);
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}