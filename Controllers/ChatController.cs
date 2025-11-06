using System;
using System.Collections.Generic;
using Online_chat.Models;
using System.Linq;
using System.Web.Mvc;
using System.IO;

namespace Online_chat.Controllers
{
    [Authorize]
    public class ChatController : BaseController
    {
        private readonly ApplicationDbContext _context = new ApplicationDbContext();

        public ActionResult Index(string friendUsername)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

            ViewBag.CurrentUserAvatarUrl = currentUser?.AvatarUrl;
            ViewBag.CurrentUserAvatarVersion = currentUser?.AvatarVersion ?? 0;

            ViewBag.SelectedFriendUsername = friendUsername;

            return View();
        }

        [HttpGet]
        public JsonResult GetChatHistory(string partnerUsername)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername && !u.IsDeleted);
            var partnerUser = _context.Users.FirstOrDefault(u => u.Username == partnerUsername && !u.IsDeleted);

            if (currentUser == null || partnerUser == null)
                return Json(new { success = false, message = "Người dùng không hợp lệ" }, JsonRequestBehavior.AllowGet);

            var messages = _context.PrivateMessages
                .Where(m =>
                    (m.SenderId == currentUser.Id && m.ReceiverId == partnerUser.Id) ||
                    (m.SenderId == partnerUser.Id && m.ReceiverId == currentUser.Id))
                .OrderBy(m => m.Timestamp)
                .Select(m => new
                {
                    SenderUsername = m.Sender.Username,
                    Content = m.Content,
                    Timestamp = m.Timestamp
                })
                .ToList();

            return Json(new { success = true, messages }, JsonRequestBehavior.AllowGet);
        }
        [HttpPost]
        public JsonResult UploadFiles()
        {
            try
            {
                var uploadedUrls = new List<string>();
                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".mp4", ".mov", ".webm", ".pdf", ".docx", ".xlsx", ".zip" };
                var maxSize = 20 * 1024 * 1024; // 20MB

                foreach (string fileKey in Request.Files)
                {
                    var file = Request.Files[fileKey];
                    if (file == null || file.ContentLength == 0 || file.ContentLength > maxSize) continue;

                    var ext = Path.GetExtension(file.FileName).ToLower();
                    if (!allowedExtensions.Contains(ext)) continue;

                    var fileName = Guid.NewGuid() + ext;
                    var uploadsDir = Server.MapPath("~/Uploads/Files/");
                    if (!Directory.Exists(uploadsDir))
                        Directory.CreateDirectory(uploadsDir);

                    var path = Path.Combine(uploadsDir, fileName);
                    file.SaveAs(path);

                    var timestamp = DateTime.Now.Ticks;
                    uploadedUrls.Add($"/Uploads/Files/{fileName}?v={timestamp}");
                }

                return Json(new { success = uploadedUrls.Any(), urls = uploadedUrls });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Upload lỗi: " + ex.Message });
            }
        }


    }
}