using Online_chat.Models;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using BCrypt.Net;

namespace Online_chat.Controllers
{
    [Authorize]
    public class SettingsController : BaseController
    {
        private readonly ApplicationDbContext _context = new ApplicationDbContext();

        [HttpGet]
        public ActionResult Index(string page = "Appearance")
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);
            ViewBag.CurrentUserAvatarUrl = currentUser?.AvatarUrl;
            ViewBag.CurrentUserAvatarVersion = currentUser?.AvatarVersion ?? 0;

            ViewBag.ActivePage = page;

            switch (page)
            {
                case "Block":
                    var blockedUsers = _context.BlockedUsers
                                            .Where(bu => bu.UserId == currentUser.Id)
                                            .Include(bu => bu.BlockedUserEntity)
                                            .ToList();
                    ViewBag.BlockedUsers = blockedUsers;
                    break;
                // Add other cases if they need specific data
                case "Appearance":
                case "Notifications":
                case "Security":
                case "Language":
                default:
                    break;
            }

            return View();
        }


        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            var currentUsername = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (user == null)
            {
                return Json(new { success = false, message = "Lỗi: Người dùng không tồn tại." });
            }
            if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
            {
                return Json(new { success = false, message = "Mật khẩu cũ không chính xác." });
            }
            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                return Json(new { success = false, message = "Mật khẩu mới phải có ít nhất 6 ký tự." });
            }

            if (newPassword != confirmPassword)
            {
                return Json(new { success = false, message = "Mật khẩu xác nhận không khớp." });
            }
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            _context.SaveChanges();

            return Json(new { success = true, message = "Đổi mật khẩu thành công!" });
        }




        [HttpPost]
        public JsonResult UnblockUser(int blockedUserId)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);
            var blockedUser = _context.BlockedUsers.FirstOrDefault(bu => bu.BlockedUserId == blockedUserId && bu.UserId == currentUser.Id);

            if (blockedUser != null)
            {
                _context.BlockedUsers.Remove(blockedUser);
                _context.SaveChanges();
                return Json(new { success = true });
            }

            return Json(new { success = false, message = "Không tìm thấy người dùng này trong danh sách chặn." });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult BlockUser(int friendId)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (currentUser == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            // Check if already blocked
            var isAlreadyBlocked = _context.BlockedUsers.Any(bu => bu.UserId == currentUser.Id && bu.BlockedUserId == friendId);

            if (isAlreadyBlocked)
            {
                return Json(new { success = false, message = "Bạn đã chặn người này rồi." });
            }

            var blockedUser = new BlockedUser
            {
                UserId = currentUser.Id,
                BlockedUserId = friendId
            };

            _context.BlockedUsers.Add(blockedUser);
            _context.SaveChanges();

            return Json(new { success = true, message = "Đã chặn người dùng." });
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