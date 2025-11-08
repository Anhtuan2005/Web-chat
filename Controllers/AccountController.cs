using Microsoft.Ajax.Utilities;
using Online_chat.Models;
using System;
using System.Linq;
using System.Web.Mvc;
using System.Web.Security;


namespace Online_chat.Controllers
{
    public class AccountController : Controller
    {
        private readonly ApplicationDbContext _context = new ApplicationDbContext();

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Register()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public ActionResult Register(string username, string password, string displayName, string email, string phoneNumber)
        {
            if (string.IsNullOrWhiteSpace(username) || string.IsNullOrWhiteSpace(password) ||
                string.IsNullOrWhiteSpace(displayName) || string.IsNullOrWhiteSpace(email))
            {
                ViewBag.Error = "Vui lòng điền đầy đủ các trường bắt buộc.";
                return View();
            }

            if (_context.Users.Any(u => u.Email == email))
            {
                ViewBag.Error = "Email đã được sử dụng.";
                return View();
            }

            Random random = new Random();
            string userCode;
            do
            {
                userCode = random.Next(10000000, 99999999).ToString();
            }
         
            while (_context.Users.Any(u => u.UserCode == userCode));

            string hashedPassword = BCrypt.Net.BCrypt.HashPassword(password);
            var newUser = new User
            {
                Username = username,
                PasswordHash = hashedPassword,
                DisplayName = displayName,
                Email = email,
                PhoneNumber = phoneNumber,
                UserCode = userCode,
                CreatedAt = DateTime.UtcNow
            };

            _context.Users.Add(newUser);
            _context.SaveChanges();

            return RedirectToAction("Login");
        }

        [HttpGet]
        [AllowAnonymous]
        public ActionResult Login()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        public ActionResult Login(string username, string password)
        {
            var user = _context.Users.FirstOrDefault(u => u.Username == username && u.IsDeleted == false);

            if (user != null && BCrypt.Net.BCrypt.Verify(password, user.PasswordHash))
            {
                FormsAuthentication.SetAuthCookie(username, false);

                if (user.IsAdmin)
                {
                    return RedirectToAction("Index", "Admin");
                }
                else
                {
                    return RedirectToAction("Index", "Chat");
                }
            }

            ViewBag.Error = "Tên đăng nhập hoặc mật khẩu không đúng.";
            return View();
        }

        // POST: /Account/Logout
        public ActionResult Logout()
        {
            FormsAuthentication.SignOut();
            return RedirectToAction("Login");
        }
    }
}