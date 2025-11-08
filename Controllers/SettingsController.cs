using Online_chat.Models; 
using System.Linq;
using System.Web.Mvc;
using BCrypt.Net; 

namespace Online_chat.Controllers
{
    [Authorize] 
    public class SettingsController : BaseController
    {
        private readonly ApplicationDbContext _context = new ApplicationDbContext();

        [HttpGet]
        public ActionResult Index()
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);
            ViewBag.CurrentUserAvatarUrl = currentUser?.AvatarUrl;
            ViewBag.CurrentUserAvatarVersion = currentUser?.AvatarVersion ?? 0;

            return View();
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ChangePassword(string oldPassword, string newPassword, string confirmPassword)
        {
            var currentUsername = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (user == null)
            {
                return HttpNotFound();
            }
            if (!BCrypt.Net.BCrypt.Verify(oldPassword, user.PasswordHash))
            {
                TempData["PasswordError"] = "Mật khẩu cũ không chính xác.";
                return RedirectToAction("Index");
            }
            if (string.IsNullOrEmpty(newPassword) || newPassword.Length < 6)
            {
                TempData["PasswordError"] = "Mật khẩu mới phải có ít nhất 6 ký tự.";
                return RedirectToAction("Index");
            }

            if (newPassword != confirmPassword)
            {
                TempData["PasswordError"] = "Mật khẩu xác nhận không khớp.";
                return RedirectToAction("Index");
            }
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(newPassword);
            _context.SaveChanges();

            TempData["PasswordSuccess"] = "Đổi mật khẩu thành công!";
            return RedirectToAction("Index");
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