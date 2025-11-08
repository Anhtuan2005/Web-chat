using Microsoft.AspNet.Identity;
using Microsoft.AspNet.SignalR;
using Online_chat.Hubs;
using Online_chat.Models;
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

            if (!string.IsNullOrEmpty(searchTerm))
            {
                int.TryParse(searchTerm, out int searchId);

                // SỬA LỖI 3: Lọc ra user đã bị xóa (u.IsDeleted == false)
                var foundUsers = _context.Users
                    .Where(u => u.Id != currentUserId &&
                                u.IsDeleted == false && // <--- THÊM ĐIỀU KIỆN NÀY
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
                Status = FriendshipStatus.Pending
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
        public JsonResult GetFriendsList()
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername && u.IsDeleted == false);

            if (currentUser == null)
            {
                return Json(new List<FriendInfoViewModel>(), JsonRequestBehavior.AllowGet);
            }
            var currentUserId = currentUser.Id;

            var friendships = _context.Friendships
                .Where(f => f.Status == FriendshipStatus.Accepted &&
                            (
                                (f.SenderId == currentUserId && f.Receiver.IsDeleted == false) ||
                                (f.ReceiverId == currentUserId && f.Sender.IsDeleted == false)  
                            ))
                .Include(f => f.Sender)
                .Include(f => f.Receiver)
                .ToList();

            var friendsViewModel = friendships.Select(f => {
                var friendUser = f.SenderId == currentUserId ? f.Receiver : f.Sender;

                return new FriendInfoViewModel
                {
                    Id = friendUser.Id,
                    Username = friendUser.Username,
                    DisplayName = friendUser.DisplayName,
                    AvatarUrl = friendUser.AvatarUrl
                };
            }).ToList();

            return Json(friendsViewModel, JsonRequestBehavior.AllowGet);
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