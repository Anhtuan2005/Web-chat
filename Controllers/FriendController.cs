using Microsoft.AspNet.Identity;
using Microsoft.AspNet.SignalR;
using Online_chat.Hubs;
using Online_chat.Models;
using System;
using System.Collections.Generic;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Online_chat.Controllers
{
    [System.Web.Mvc.Authorize]
    public class FriendController : BaseController
    {
        // Ghi chú: _context đã được kế thừa từ BaseController
        // private readonly ApplicationDbContext _context = new ApplicationDbContext();

        public ActionResult Index()
        {
            var currentUsername = User.Identity.Name;

            // SỬA LỖI: Thêm .Where(u => u.IsDeleted == false)
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername && u.IsDeleted == false);
            if (currentUser == null)
            {
                return new HttpUnauthorizedResult();
            }

            ViewBag.CurrentUserAvatarUrl = currentUser.AvatarUrl;
            ViewBag.CurrentUserAvatarVersion = currentUser.AvatarVersion;

            var currentUserId = currentUser.Id;

            // SỬA LỖI 1: Lọc ra các lời mời từ user đã bị xóa (f.Sender.IsDeleted == false)
            var pendingRequests = _context.Friendships
                .Where(f => f.ReceiverId == currentUserId &&
                            f.Status == FriendshipStatus.Pending &&
                            f.Sender.IsDeleted == false) // <--- THÊM ĐIỀU KIỆN NÀY
                .Include(f => f.Sender)
                .Select(f => new FriendRequestViewModel
                {
                    FriendshipId = f.Id,
                    SenderDisplayName = f.Sender.DisplayName,
                    SenderUsername = f.Sender.Username
                })
                .ToList();

            // SỬA LỖI 2: Lọc ra bạn bè đã bị xóa
            var friendships = _context.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted &&
                            (
                                (f.SenderId == currentUserId && f.Receiver.IsDeleted == false) || // Kiểm tra người nhận
                                (f.ReceiverId == currentUserId && f.Sender.IsDeleted == false)    // Kiểm tra người gửi
                            ))
                .Include(f => f.Sender)
                .Include(f => f.Receiver)
                .ToList();

            var friends = friendships.Select(f => {
                var friendUser = (f.SenderId == currentUserId) ? f.Receiver : f.Sender;

                return new FriendItemViewModel
                {
                    FriendshipId = f.Id,
                    FriendId = friendUser.Id,
                    FriendDisplayName = friendUser.DisplayName,
                    FriendUsername = friendUser.Username,
                    FriendAvatarUrl = friendUser.AvatarUrl 
                };
            }).ToList();

            var viewModel = new FriendshipPageViewModel
            {
                Friends = friends,
                PendingRequests = pendingRequests
            };

            return View(viewModel);
        }

        public ActionResult Search(string searchTerm)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername && u.IsDeleted == false); // Đã lọc IsDeleted ở đây
            if (currentUser == null) return View(new List<SearchResultViewModel>());

            var currentUserId = currentUser.Id;
            var results = new List<SearchResultViewModel>();

            var blockedUserIds = _context.BlockedUsers
                .Where(b => b.BlockerId == currentUserId || b.BlockedId == currentUserId)
                .Select(b => b.BlockerId == currentUserId ? b.BlockedId : b.BlockerId)
                .ToHashSet();

            if (!string.IsNullOrEmpty(searchTerm))
            {
                int.TryParse(searchTerm, out int searchId);

                // SỬA LỖI 3: Lọc ra user đã bị xóa (u.IsDeleted == false) và user đã bị chặn
                var foundUsers = _context.Users
                    .Where(u => u.Id != currentUserId &&
                                !u.IsDeleted &&
                                !blockedUserIds.Contains(u.Id) && 
                                (u.PhoneNumber == searchTerm || u.UserCode == searchTerm || (searchId != 0 && u.Id == searchId)))
                    .ToList();

                var foundUserIds = foundUsers.Select(u => u.Id).ToList();

                var existingFriendships = _context.Friendships
                    .Where(f => (f.SenderId == currentUserId && foundUserIds.Contains(f.ReceiverId)) ||
                                (f.ReceiverId == currentUserId && foundUserIds.Contains(f.SenderId)))
                    .ToList();

                foreach (var user in foundUsers)
                {
                    var resultItem = new SearchResultViewModel
                    {
                        UserId = user.Id,
                        DisplayName = user.DisplayName,
                        Username = user.Username,
                        AvatarUrl = user.AvatarUrl,
                        CoverPhotoUrl = user.CoverPhotoUrl,
                        UserCode = user.UserCode,
                        FriendshipStatus = null,

                        Gender = user.Gender,
                        DateOfBirth = user.DateOfBirth, 
                        Bio = user.Bio,

                        PhoneNumber = string.IsNullOrEmpty(user.PhoneNumber) ? "" : "**********",
                        Email = string.IsNullOrEmpty(user.Email) ? "" : "**********"
                    };

                    var friendship = existingFriendships.FirstOrDefault(f =>
                        (f.SenderId == user.Id || f.ReceiverId == user.Id));

                    if (friendship != null)
                    {
                        resultItem.FriendshipStatus = friendship.Status;
                    }
                    results.Add(resultItem);
                }
            }
            return View(results);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult SendRequest(int receiverId)
        {
            var senderUsername = User.Identity.Name;
            var sender = _context.Users.FirstOrDefault(u => u.Username == senderUsername && u.IsDeleted == false); // Đã lọc IsDeleted
            if (sender == null)
            {
                return Json(new { success = false, message = "Lỗi xác thực người gửi." });
            }
            var senderId = sender.Id;

            if (senderId == receiverId)
            {
                return Json(new { success = false, message = "Bạn không thể tự kết bạn với chính mình." });
            }

            var existingFriendship = _context.Friendships.FirstOrDefault(f =>
                (f.SenderId == senderId && f.ReceiverId == receiverId) ||
                (f.SenderId == receiverId && f.ReceiverId == senderId));

            if (existingFriendship != null)
            {
                if (existingFriendship.Status == FriendshipStatus.Accepted)
                {
                    return Json(new { success = false, message = "Bạn đã là bạn bè với người này." });
                }
                else if (existingFriendship.Status == FriendshipStatus.Pending)
                {
                    if (existingFriendship.SenderId == senderId)
                    {
                        return Json(new { success = false, message = "Bạn đã gửi lời mời kết bạn cho người này rồi." });
                    }
                    else
                    {
                        return Json(new { success = false, message = "Người này đã gửi lời mời cho bạn. Hãy kiểm tra danh sách lời mời." });
                    }
                }
            }

            var friendship = new Friendship
            {
                SenderId = senderId,
                ReceiverId = receiverId,
                Status = FriendshipStatus.Pending,
                CreatedAt = DateTime.Now
            };
            _context.Friendships.Add(friendship);
            _context.SaveChanges();

            var receiver = _context.Users.FirstOrDefault(u => u.Id == receiverId && u.IsDeleted == false); // Đã lọc IsDeleted
            if (receiver != null)
            {
                var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
                hubContext.Clients.User(receiver.Username).receiveFriendRequestNotification(sender.DisplayName);
            }

            return Json(new { success = true, action = "sent" });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult CancelRequest(int receiverId)
        {
            var senderUsername = User.Identity.Name;
            var sender = _context.Users.FirstOrDefault(u => u.Username == senderUsername && u.IsDeleted == false); // Đã lọc IsDeleted
            if (sender == null)
            {
                return Json(new { success = false, message = "Lỗi xác thực." });
            }

            var friendship = _context.Friendships.FirstOrDefault(f =>
                f.SenderId == sender.Id && f.ReceiverId == receiverId && f.Status == FriendshipStatus.Pending);

            if (friendship != null)
            {
                _context.Friendships.Remove(friendship);
                _context.SaveChanges();
                return Json(new { success = true, action = "cancelled" });
            }

            return Json(new { success = false, message = "Không tìm thấy lời mời." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AcceptRequest(int friendshipId)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername && u.IsDeleted == false);
            if (currentUser == null) return new HttpUnauthorizedResult();
            var currentUserId = currentUser.Id;
            var friendship = _context.Friendships
                .Include(f => f.Sender) // Tải thông tin người gửi
                .FirstOrDefault(f => f.Id == friendshipId && f.ReceiverId == currentUserId);

            if (friendship != null && friendship.Status == FriendshipStatus.Pending)
            {
                // Kiểm tra xem người gửi có bị xóa không
                if (friendship.Sender.IsDeleted)
                {
                    TempData["ErrorMessage"] = "Không thể chấp nhận. Người dùng này đã bị xóa.";
                    _context.Friendships.Remove(friendship); // Xóa luôn lời mời rác
                    _context.SaveChanges();
                    return RedirectToAction("Index");
                }

                friendship.Status = FriendshipStatus.Accepted;
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Chấp nhận lời mời kết bạn thành công.";
            }

            return RedirectToAction("Index");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeclineOrRemoveFriend(int friendshipId)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername && u.IsDeleted == false);
            if (currentUser == null) return new HttpUnauthorizedResult();
            var currentUserId = currentUser.Id;
            var friendship = _context.Friendships.FirstOrDefault(f => f.Id == friendshipId && (f.SenderId == currentUserId || f.ReceiverId == currentUserId));

            if (friendship != null)
            {
                _context.Friendships.Remove(friendship);
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Đã xóa mối quan hệ bạn bè.";
            }

            return RedirectToAction("Index");
        }

        [HttpGet]
        public JsonResult GetFriends()
        {
            try
            {
                var currentUsername = User.Identity.Name;
                var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername && u.IsDeleted == false);

                if (currentUser == null)
                {
                    return Json(new { success = false, message = "User not found" }, JsonRequestBehavior.AllowGet);
                }

                var currentUserId = currentUser.Id;

                var friendships = _context.Friendships
                    .Include(f => f.Sender)
                    .Include(f => f.Receiver)
                    .Where(f => f.Status == FriendshipStatus.Accepted &&
                               (
                                   (f.SenderId == currentUserId && f.Receiver.IsDeleted == false) ||
                                   (f.ReceiverId == currentUserId && f.Sender.IsDeleted == false)
                               ))
                    .ToList();

                var friends = friendships.Select(f =>
                {
                    var friendUser = f.SenderId == currentUserId ? f.Receiver : f.Sender;

                    var lastMessage = _context.PrivateMessages
                        .Where(m => (m.SenderId == currentUserId && m.ReceiverId == friendUser.Id) ||
                                   (m.SenderId == friendUser.Id && m.ReceiverId == currentUserId))
                        .OrderByDescending(m => m.Timestamp)
                        .FirstOrDefault();

                    var unreadCount = _context.PrivateMessages
                        .Count(m => m.SenderId == friendUser.Id &&
                                   m.ReceiverId == currentUserId &&
                                   !m.IsRead);

                    return new
                    {
                        Id = friendUser.Id,
                        Username = friendUser.Username,
                        DisplayName = friendUser.DisplayName ?? friendUser.Username,
                        AvatarUrl = friendUser.AvatarUrl ?? "/Content/default-avatar.png",
                        LastMessage = lastMessage != null ? GetMessagePreview(lastMessage.Content) : null,
                        LastMessageTime = lastMessage?.Timestamp,
                        UnreadCount = unreadCount
                    };
                })
                .OrderByDescending(f => f.LastMessageTime ?? DateTime.MinValue)
                .ToList();

                return Json(new { success = true, friends = friends }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // Helper method để lấy preview của tin nhắn
        private string GetMessagePreview(string messageContent)
        {
            try
            {
                // Parse JSON content
                var obj = Newtonsoft.Json.JsonConvert.DeserializeObject<dynamic>(messageContent);
                var type = obj?.type?.ToString();

                switch (type)
                {
                    case "text":
                        var content = obj?.content?.ToString();
                        if (string.IsNullOrEmpty(content)) return "";
                        return content.Length > 50 ? content.Substring(0, 50) + "..." : content;

                    case "image":
                        return "📷 Hình ảnh";

                    case "video":
                        return "🎥 Video";

                    case "file":
                        return "📎 File đính kèm";

                    case "call_log":
                        return "📞 Cuộc gọi";

                    default:
                        // Nếu không parse được JSON, trả về text thuần
                        if (string.IsNullOrEmpty(messageContent)) return "";
                        return messageContent.Length > 50
                            ? messageContent.Substring(0, 50) + "..."
                            : messageContent;
                }
            }
            catch
            {
                // Fallback: nếu không parse được, coi như text thuần
                if (string.IsNullOrEmpty(messageContent)) return "";
                return messageContent.Length > 50
                    ? messageContent.Substring(0, 50) + "..."
                    : messageContent;
            }
        }

        [HttpGet]
        public JsonResult GetFriendSuggestions()
        {
            try
            {
                var currentUsername = User.Identity.Name;
                var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername && !u.IsDeleted);
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "User not found." }, JsonRequestBehavior.AllowGet);
                }
                var currentUserId = currentUser.Id;

                // 1. Get IDs of current user's direct friends
                var friendIds = _context.Friendships
                    .Where(f => f.Status == FriendshipStatus.Accepted && (f.SenderId == currentUserId || f.ReceiverId == currentUserId))
                    .Select(f => f.SenderId == currentUserId ? f.ReceiverId : f.SenderId)
                    .ToList();

                // 2. Get IDs of users with pending requests (sent or received)
                var pendingIds = _context.Friendships
                    .Where(f => f.Status == FriendshipStatus.Pending && (f.SenderId == currentUserId || f.ReceiverId == currentUserId))
                    .Select(f => f.SenderId == currentUserId ? f.ReceiverId : f.SenderId)
                    .ToList();

                // 3. Combine all excluded IDs: self, friends, and pending
                var excludedIds = new HashSet<int>(friendIds);
                excludedIds.UnionWith(pendingIds);
                excludedIds.Add(currentUserId);

                // Exclude blocked users
                var blockedUserIds = _context.BlockedUsers
                    .Where(b => b.BlockerId == currentUserId || b.BlockedId == currentUserId)
                    .Select(b => b.BlockerId == currentUserId ? b.BlockedId : b.BlockerId)
                    .ToList();
                excludedIds.UnionWith(blockedUserIds);

                if (!friendIds.Any())
                {
                    // If user has no friends, suggest random users they don't have any relationship with
                                                            var randomUsers = _context.Users
                                                                .Where(u => !excludedIds.Contains(u.Id) && !u.IsDeleted && !u.IsAdmin && u.Username != "Admin")                        .ToList() // Fetch to memory
                        .OrderBy(u => Guid.NewGuid()) // Random order in memory
                        .Take(5)
                        .Select(u => new {
                            u.Id,
                            u.Username,
                            DisplayName = u.DisplayName ?? u.Username,
                            AvatarUrl = u.AvatarUrl ?? "/Content/default-avatar.png"
                        })
                        .ToList();
                    return Json(new { success = true, suggestions = randomUsers }, JsonRequestBehavior.AllowGet);
                }

                // 4. Get IDs of friends of friends
                var friendsOfFriendsIds = _context.Friendships
                    .Where(f => f.Status == FriendshipStatus.Accepted && (friendIds.Contains(f.SenderId) || friendIds.Contains(f.ReceiverId)))
                    .Select(f => friendIds.Contains(f.SenderId) ? f.ReceiverId : f.SenderId)
                    .Distinct()
                    .ToList();

                // 5. Filter out excluded IDs
                var suggestedUserIds = friendsOfFriendsIds
                    .Where(id => !excludedIds.Contains(id))
                    .ToList();

                // 6. Fetch user details for the suggestions, then randomize in memory
                                                var suggestions = _context.Users
                                                    .Where(u => suggestedUserIds.Contains(u.Id) && !u.IsDeleted && !u.IsAdmin && u.Username != "Admin")                    .ToList() // Fetch to memory
                    .OrderBy(u => Guid.NewGuid()) // Randomize in memory
                    .Take(5) // Limit to 5 suggestions
                    .Select(u => new {
                        u.Id,
                        u.Username,
                        DisplayName = u.DisplayName ?? u.Username,
                        AvatarUrl = u.AvatarUrl ?? "/Content/default-avatar.png"
                    })
                    .ToList();

                // If not enough suggestions from friends-of-friends, fill with random users
                if (suggestions.Count < 5)
                {
                    var currentSuggestionIds = new HashSet<int>(suggestions.Select(s => s.Id));
                    var allExcludedIds = new HashSet<int>(excludedIds);
                    allExcludedIds.UnionWith(currentSuggestionIds);

                                                            var additionalUsers = _context.Users

                                                                .Where(u => !allExcludedIds.Contains(u.Id) && !u.IsDeleted && !u.IsAdmin && u.Username != "Admin")                        .ToList() // Fetch to memory
                        .OrderBy(u => Guid.NewGuid()) // Randomize in memory
                        .Take(5 - suggestions.Count)
                        .Select(u => new {
                            u.Id,
                            u.Username,
                            DisplayName = u.DisplayName ?? u.Username,
                            AvatarUrl = u.AvatarUrl ?? "/Content/default-avatar.png"
                        })
                        .ToList();
                    suggestions.AddRange(additionalUsers);
                }


                return Json(new { success = true, suggestions = suggestions }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "An error occurred: " + ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        [HttpGet]
        public JsonResult GetUserDetails(int userId)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == userId && !u.IsDeleted);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found." }, JsonRequestBehavior.AllowGet);
            }

            var profileData = new
            {
                user.Id,
                user.Username,
                DisplayName = user.DisplayName ?? user.Username,
                AvatarUrl = user.AvatarUrl ?? "/Content/default-avatar.png",
                CoverPhotoUrl = user.CoverPhotoUrl ?? "/Content/default-cover.jpg",
                user.Bio,
                Gender = user.Gender?.ToString() ?? "Chưa cập nhật",
                DateOfBirth = user.DateOfBirth?.ToString("dd/MM/yyyy") ?? "Chưa cập nhật",
                Email = string.IsNullOrEmpty(user.Email) ? "Chưa cập nhật" : "**********",
                PhoneNumber = string.IsNullOrEmpty(user.PhoneNumber) ? "Chưa cập nhật" : "**********"
            };

            return Json(new { success = true, user = profileData }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult BlockUser(int blockedId)
        {
            if (blockedId <= 0)
            {
                return Json(new { success = false, message = "ID người dùng không hợp lệ." });
            }
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (currentUser == null)
            {
                return Json(new { success = false, message = "Không thể xác thực người dùng." });
            }

            if (currentUser.Id == blockedId)
            {
                return Json(new { success = false, message = "Bạn không thể tự chặn chính mình." });
            }

            // Check if block already exists
            var alreadyBlocked = _context.BlockedUsers.Any(b => b.BlockerId == currentUser.Id && b.BlockedId == blockedId);
            if (alreadyBlocked)
            {
                return Json(new { success = true, message = "Người dùng đã bị chặn từ trước." });
            }

            // Create new block record
            var newBlock = new BlockedUser
            {
                BlockerId = currentUser.Id,
                BlockedId = blockedId
            };
            _context.BlockedUsers.Add(newBlock);

            // Remove any existing friendship (sent, received, or accepted)
            var friendship = _context.Friendships.FirstOrDefault(f =>
                (f.SenderId == currentUser.Id && f.ReceiverId == blockedId) ||
                (f.SenderId == blockedId && f.ReceiverId == currentUser.Id));

            if (friendship != null)
            {
                _context.Friendships.Remove(friendship);
            }

            _context.SaveChanges();

            return Json(new { success = true, message = "Đã chặn người dùng." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult UnblockUser(int blockedId)
        {
            if (blockedId <= 0)
            {
                return Json(new { success = false, message = "ID người dùng không hợp lệ." });
            }
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);
            var blockedUser = _context.BlockedUsers.FirstOrDefault(bu => bu.BlockedId == blockedId && bu.BlockerId == currentUser.Id);

            if (blockedUser != null)
            {
                _context.BlockedUsers.Remove(blockedUser);
                _context.SaveChanges();
                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Không tìm thấy người dùng này trong danh sách chặn." });
        }


        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                _context.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}