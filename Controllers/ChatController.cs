using System;
using System.Collections.Generic;
using Online_chat.Models;
using System.Linq;
using System.Web.Mvc;
using System.IO;
using Newtonsoft.Json;
using System.Threading.Tasks;
using System.Data.Entity;

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
        public class MessageContent
        {
            public string type { get; set; }
            public string content { get; set; }
        }
        public class FileLinkInfo
        {
            public string Url { get; set; }
            public string FileName { get; set; }
            public string Timestamp { get; set; }
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
        public async Task<JsonResult> GetConversationInfo(string partnerUsername)
        {
            var currentUsername = User.Identity.Name;
            var messages = await _context.PrivateMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .Where(m =>
                    (m.Sender.Username == currentUsername && m.Receiver.Username == partnerUsername) ||
                    (m.Sender.Username == partnerUsername && m.Receiver.Username == currentUsername)
                )
                .OrderByDescending(m => m.Timestamp) // Mới nhất trước
                .ToListAsync();

            var files = new List<FileLinkInfo>();
            var images = new List<FileLinkInfo>();

            foreach (var msg in messages)
            {
                try
                {
                    var content = JsonConvert.DeserializeObject<MessageContent>(msg.Content);

                    if (content != null)
                    {
                        var timeAgo = GetTimeAgo(msg.Timestamp);

                        // 3. Phân loại File và Ảnh/Video
                        if (content.type == "file")
                        {
                            files.Add(new FileLinkInfo
                            {
                                Url = content.content,
                                FileName = System.IO.Path.GetFileName(content.content), // Lấy tên file từ URL
                                Timestamp = timeAgo
                            });
                        }
                        else if (content.type == "image" || content.type == "video")
                        {
                            images.Add(new FileLinkInfo
                            {
                                Url = content.content,
                                FileName = "Hình ảnh/Video",
                                Timestamp = timeAgo
                            });
                        }
                    }
                }
                catch (Exception)
                {
                }
            }

            return Json(new { success = true, files = files, images = images }, JsonRequestBehavior.AllowGet);
        }
        private string GetTimeAgo(DateTime timestamp)
        {
            var span = DateTime.Now - timestamp;
            if (span.Days > 365) return $"{span.Days / 365} năm trước";
            if (span.Days > 30) return $"{span.Days / 30} tháng trước";
            if (span.Days > 0) return $"{span.Days} ngày trước";
            if (span.Hours > 0) return $"{span.Hours} giờ trước";
            if (span.Minutes > 0) return $"{span.Minutes} phút trước";
            return "Vừa xong";
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public async Task<JsonResult> ClearHistory(string partnerUsername)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == currentUsername);
            var partnerUser = await _context.Users.FirstOrDefaultAsync(u => u.Username == partnerUsername);

            if (currentUser == null || partnerUser == null)
            {
                return Json(new { success = false, message = "Không tìm thấy người dùng." });
            }

            try
            {
                var messagesToDelete = _context.PrivateMessages
                    .Where(m =>
                        (m.SenderId == currentUser.Id && m.ReceiverId == partnerUser.Id) ||
                        (m.SenderId == partnerUser.Id && m.ReceiverId == currentUser.Id)
                    );

                if (await messagesToDelete.AnyAsync())
                {
                    _context.PrivateMessages.RemoveRange(messagesToDelete);
                    await _context.SaveChangesAsync();
                }

                return Json(new { success = true, message = "Đã xóa lịch sử trò chuyện." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi máy chủ: " + ex.Message });
            }
        }
    }
}