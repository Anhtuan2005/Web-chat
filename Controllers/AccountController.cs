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

                        // Check for duplicate email                                                                  
                        if (_context.Users.Any(u => u.Email == email))                                                
                        {                                                                                             
                            ViewBag.Error = "Email đã được sử dụng. Vui lòng sử dụng email khác.";                    
                            return View();                                                                            
                        }
                        
                        // Check for duplicate phone number if provided
                        if (!string.IsNullOrWhiteSpace(phoneNumber) && _context.Users.Any(u => u.PhoneNumber == phoneNumber))
                        {
                            ViewBag.Error = "Số điện thoại đã được đăng ký. Vui lòng sử dụng số điện thoại khác.";
                            return View();
                        }
            
                        Random random = new Random();            string userCode;
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
                FormsAuthentication.SetAuthCookie(user.Username, false);

                System.Diagnostics.Debug.WriteLine($"Login thành công: {user.Username}");
                System.Diagnostics.Debug.WriteLine($"User.Identity.Name: {User.Identity.Name}");

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

        [Authorize]
        [HttpGet]
        public JsonResult GetCurrentUser()
        {
            var user = _context.Users.FirstOrDefault(u => u.Username == User.Identity.Name && !u.IsDeleted);
            if (user == null)
            {
                return Json(new { success = false, message = "User not found" }, JsonRequestBehavior.AllowGet);
            }

            return Json(new
            {
                success = true,
                name = user.DisplayName,
                avatar = user.AvatarUrl ?? "/Content/default-avatar.png"
            }, JsonRequestBehavior.AllowGet);
        }

        // --- Forgot Password ---

        [HttpGet]
        [AllowAnonymous]
        public ActionResult ForgotPassword()
        {
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ForgotPassword(string email)
        {
            var user = _context.Users.FirstOrDefault(u => u.Email == email && !u.IsDeleted);

            if (user != null)
            {
                // Generate a unique, secure token
                string token = Guid.NewGuid().ToString() + Guid.NewGuid().ToString();
                token = token.Replace("-", "");

                user.PasswordResetToken = token;
                user.ResetTokenExpiration = DateTime.UtcNow.AddHours(1); // Token is valid for 1 hour
                _context.SaveChanges();

                // Generate the password reset link
                var resetLink = Url.Action("ResetPassword", "Account", new { token = token }, Request.Url.Scheme);

                // Send the email (placeholder)
                var emailService = new EmailService();
                emailService.SendPasswordResetEmail(user.Email, resetLink);
            }

            // Always show a generic confirmation message to prevent email enumeration
            ViewBag.Message = "Nếu địa chỉ email của bạn tồn tại trong hệ thống, một liên kết khôi phục mật khẩu đã được gửi đi.";
            return View();
        }


        [HttpGet]
        [AllowAnonymous]
        public ActionResult ResetPassword(string token)
        {
            if (string.IsNullOrWhiteSpace(token))
            {
                return HttpNotFound();
            }

            var user = _context.Users.FirstOrDefault(u => u.PasswordResetToken == token && u.ResetTokenExpiration > DateTime.UtcNow);

            if (user == null)
            {
                ViewBag.Error = "Liên kết khôi phục mật khẩu không hợp lệ hoặc đã hết hạn.";
                return View("ForgotPassword"); // Redirect to forgot password with an error
            }

            ViewBag.Token = token;
            return View();
        }

        [HttpPost]
        [AllowAnonymous]
        [ValidateAntiForgeryToken]
        public ActionResult ResetPassword(string token, string password, string confirmPassword)
        {
            if (string.IsNullOrWhiteSpace(token) || string.IsNullOrWhiteSpace(password) || string.IsNullOrWhiteSpace(confirmPassword))
            {
                ViewBag.Token = token;
                ViewBag.Error = "Vui lòng điền đầy đủ thông tin.";
                return View();
            }

            if (password != confirmPassword)
            {
                ViewBag.Token = token;
                ViewBag.Error = "Mật khẩu và xác nhận mật khẩu không khớp.";
                return View();
            }

            var user = _context.Users.FirstOrDefault(u => u.PasswordResetToken == token && u.ResetTokenExpiration > DateTime.UtcNow);

            if (user == null)
            {
                ViewBag.Error = "Liên kết khôi phục mật khẩu không hợp lệ hoặc đã hết hạn. Vui lòng thử lại.";
                return View("ForgotPassword");
            }

            // Hash the new password
            user.PasswordHash = BCrypt.Net.BCrypt.HashPassword(password);
            
            // Invalidate the token
            user.PasswordResetToken = null;
            user.ResetTokenExpiration = null;
            
            _context.SaveChanges();

            // Redirect to login page with a success message
            TempData["SuccessMessage"] = "Mật khẩu của bạn đã được đặt lại thành công. Vui lòng đăng nhập.";
            return RedirectToAction("Login");
        }
    }

        // Placeholder Email Service                                                                          

        public class EmailService

        {

            public void SendPasswordResetEmail(string toEmail, string resetLink)

            {

                try

                {

                    var smtpClient = new System.Net.Mail.SmtpClient();

                    var mailMessage = new System.Net.Mail.MailMessage

                    {

                        // From address is picked up from Web.config

                        Subject = "Yêu cầu đặt lại mật khẩu - WebChat Online",

                        Body = $"<p>Chào bạn,</p><p>Bạn đã yêu cầu đặt lại mật khẩu cho tài khoản của mình trên WebChat Online.</p><p>Vui lòng nhấn vào liên kết sau để đặt lại mật khẩu của bạn: <a href='{resetLink}'>Đặt lại mật khẩu</a></p><p>Nếu bạn không yêu cầu điều này, vui lòng bỏ qua email này.</p><p>Trân trọng,<br>Đội ngũ WebChat Online</p>",

                        IsBodyHtml = true

                    };

                    mailMessage.To.Add(new System.Net.Mail.MailAddress(toEmail));

    

                    smtpClient.Send(mailMessage);

    

                    System.Diagnostics.Debug.WriteLine("--- PASSWORD RESET EMAIL SENT ---");

                    System.Diagnostics.Debug.WriteLine($"To: {toEmail}");

                }

                catch (System.Exception ex)

                {

                    // Log the exception to the debug output

                    System.Diagnostics.Debug.WriteLine("--- EMAIL SENDING FAILED ---");

                    System.Diagnostics.Debug.WriteLine("Error sending password reset email: " + ex.ToString());

                    // In a real application, you would log this to a file or a logging service.

                }

            }

        }
}