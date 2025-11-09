using Online_chat.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;
using System.Collections.Generic;

namespace Online_chat.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context = new ApplicationDbContext();

        // ============================================
        // DASHBOARD (Trang chủ Admin)
        // ============================================
        public ActionResult Index()
        {
            // Thống kê tổng quan
            ViewBag.UserCount = _context.Users.Count(u => !u.IsDeleted);
            ViewBag.GroupCount = _context.Groups.Count();
            ViewBag.MessageCount = _context.Messages.Count();
            ViewBag.PrivateMessageCount = _context.PrivateMessages.Count();

            // Thống kê 30 ngày qua
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            ViewBag.NewUsersLast30Days = _context.Users.Count(u => u.CreatedAt >= thirtyDaysAgo && !u.IsDeleted);
            ViewBag.MessagesLast30Days = _context.Messages.Count(m => m.Timestamp >= thirtyDaysAgo);

            ViewBag.RecentUsers = _context.Users
            .OrderByDescending(u => u.CreatedAt)
            .Take(5)
            .ToList();

            ViewBag.ActiveGroups = _context.Groups
            .Include(g => g.Messages) 
            .OrderByDescending(g => g.Messages.Count())
            .Take(5)
            .ToList();

            return View();
        }

        // ============================================
        // QUẢN LÝ USERS
        // ============================================
        public ActionResult ManageUsers(string search, int page = 1, int pageSize = 20)
        {
            var query = _context.Users.Where(u => !u.IsDeleted);

            // Tìm kiếm
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(u =>
                    u.Username.ToLower().Contains(search) ||
                    u.Email.ToLower().Contains(search) ||
                    u.DisplayName.ToLower().Contains(search)
                );
                ViewBag.SearchQuery = search;
            }

            // Phân trang
            int totalUsers = query.Count();
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalUsers / pageSize);
            ViewBag.CurrentPage = page;

            var users = query
                .OrderByDescending(u => u.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return View(users);
        }

        public ActionResult EditUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null || user.IsDeleted)
            {
                return HttpNotFound();
            }
            return View(user);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditUser([Bind(Include = "Id,DisplayName,Email,PhoneNumber,IsAdmin,Username,PasswordHash,CreatedAt")] User user)
        {
            if (ModelState.IsValid)
            {
                var existingUser = _context.Users.AsNoTracking().FirstOrDefault(u => u.Id == user.Id);
                if (existingUser != null)
                {
                    user.Username = existingUser.Username;
                    user.PasswordHash = existingUser.PasswordHash;
                    user.CreatedAt = existingUser.CreatedAt;
                    user.IsDeleted = existingUser.IsDeleted;
                }

                _context.Entry(user).State = EntityState.Modified;
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Cập nhật thông tin người dùng thành công!";
                return RedirectToAction("ManageUsers");
            }
            return View(user);
        }

        // ✅ KHÓA USER (Thay vì xóa)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult BanUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null) return HttpNotFound();

            if (user.Username.ToLower() == User.Identity.Name.ToLower())
            {
                TempData["ErrorMessage"] = "Bạn không thể khóa chính mình!";
                return RedirectToAction("ManageUsers");
            }

            user.IsDeleted = true;
            _context.Entry(user).State = EntityState.Modified;
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã khóa tài khoản: {user.Username}";
            return RedirectToAction("ManageUsers");
        }

        // ✅ MỞ KHÓA USER
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult UnbanUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null) return HttpNotFound();

            user.IsDeleted = false;
            _context.Entry(user).State = EntityState.Modified;
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã mở khóa tài khoản: {user.Username}";
            return RedirectToAction("ManageUsers");
        }

        // Danh sách User bị khóa
        public ActionResult BannedUsers()
        {
            var bannedUsers = _context.Users.Where(u => u.IsDeleted).ToList();
            return View(bannedUsers);
        }

        // ============================================
        // QUẢN LÝ NHÓM
        // ============================================
        public ActionResult ManageGroups(string search)
        {
            var query = _context.Groups.Include(g => g.Owner).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(g => g.GroupName.ToLower().Contains(search));
                ViewBag.SearchQuery = search;
            }

            var groups = query.OrderByDescending(g => g.CreatedAt).ToList();
            return View(groups);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteGroup(int id)
        {
            var group = _context.Groups.Find(id);
            if (group == null) return HttpNotFound();

            var messagesInGroup = _context.Messages.Where(m => m.GroupId == id);
            _context.Messages.RemoveRange(messagesInGroup);

            _context.Groups.Remove(group);
            _context.SaveChanges();
            TempData["SuccessMessage"] = $"Đã xóa nhóm: {group.GroupName}";

            return RedirectToAction("ManageGroups");
        }

        public ActionResult EditGroup(int id)
        {
            var group = _context.Groups.Find(id);
            if (group == null) return HttpNotFound();

            // Lấy danh sách user để chọn Owner mới
            ViewBag.Users = _context.Users
                .Where(u => !u.IsDeleted)
                .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.DisplayName })
                .ToList();

            return View(group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditGroup([Bind(Include = "Id,GroupName,CreatedAt,OwnerId")] Group group)
        {
            if (ModelState.IsValid)
            {
                _context.Entry(group).State = EntityState.Modified;
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Cập nhật thông tin nhóm thành công!";
                return RedirectToAction("ManageGroups");
            }

            ViewBag.Users = _context.Users
                .Where(u => !u.IsDeleted)
                .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.DisplayName })
                .ToList();

            return View(group);
        }

        // ✅ XEM THÀNH VIÊN NHÓM
        public ActionResult GroupMembers(int id)
        {
            var group = _context.Groups.Find(id);
            if (group == null) return HttpNotFound();

            ViewBag.GroupName = group.GroupName;
            ViewBag.GroupId = id;

            // Lấy danh sách user đã gửi tin nhắn trong nhóm
            var members = _context.Messages
                .Where(m => m.GroupId == id)
                .Select(m => m.Sender)
                .Distinct()
                .ToList();

            return View(members);
        }

        // ============================================
        // QUẢN LÝ TIN NHẮN NHÓM
        // ============================================
        public ActionResult ManageMessages(string search, int? groupId)
        {
            var query = _context.Messages
                .Include(m => m.Sender)
                .Include(m => m.Group)
                .AsQueryable();

            // Lọc theo nhóm
            if (groupId.HasValue)
            {
                query = query.Where(m => m.GroupId == groupId.Value);
                ViewBag.SelectedGroupId = groupId.Value;
            }

            // Tìm kiếm
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(m => m.Content.ToLower().Contains(search));
                ViewBag.SearchQuery = search;
            }

            var messages = query
                .OrderByDescending(m => m.Timestamp)
                .Take(100)
                .ToList();

            ViewBag.Groups = _context.Groups
                .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.GroupName })
                .ToList();

            return View(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMessage(int id)
        {
            var message = _context.Messages.Find(id);
            if (message == null) return HttpNotFound();

            _context.Messages.Remove(message);
            _context.SaveChanges();
            TempData["SuccessMessage"] = "Đã xóa tin nhắn thành công!";

            return RedirectToAction("ManageMessages");
        }

        // ✅ XÓA TẤT CẢ TIN NHẮN CỦA MỘT USER
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteUserMessages(int userId)
        {
            var messages = _context.Messages.Where(m => m.SenderId == userId);
            var count = messages.Count();

            _context.Messages.RemoveRange(messages);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã xóa {count} tin nhắn của user này.";
            return RedirectToAction("ManageMessages");
        }

        // ============================================
        // QUẢN LÝ TIN NHẮN RIÊNG TƯ
        // ============================================
        public ActionResult ManagePrivateMessages(string search, int page = 1)
        {
            var query = _context.PrivateMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(m =>
                    m.Content.ToLower().Contains(search) ||
                    m.Sender.Username.ToLower().Contains(search) ||
                    m.Receiver.Username.ToLower().Contains(search)
                );
                ViewBag.SearchQuery = search;
            }

            int pageSize = 50;
            int totalMessages = query.Count();
            ViewBag.TotalPages = (int)Math.Ceiling((double)totalMessages / pageSize);
            ViewBag.CurrentPage = page;

            var messages = query
                .OrderByDescending(m => m.Timestamp)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            return View(messages);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeletePrivateMessage(int id)
        {
            var message = _context.PrivateMessages.Find(id);
            if (message == null) return HttpNotFound();

            _context.PrivateMessages.Remove(message);
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Đã xóa tin nhắn riêng tư.";
            return RedirectToAction("ManagePrivateMessages");
        }

        // ============================================
        // CÀI ĐẶT HỆ THỐNG
        // ============================================
        public ActionResult SystemSettings()
        {
            var settings = _context.Settings.FirstOrDefault();

            if (settings == null)
            {
                settings = new Setting
                {
                    SiteName = "Online Chat",
                    AllowNewRegistrations = true
                };
                _context.Settings.Add(settings);
                _context.SaveChanges();
            }

            return View(settings);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SystemSettings(Setting settings)
        {
            if (ModelState.IsValid)
            {
                _context.Entry(settings).State = EntityState.Modified;
                _context.SaveChanges();
                TempData["SuccessMessage"] = "Đã cập nhật cài đặt thành công!";
                return RedirectToAction("SystemSettings");
            }
            return View(settings);
        }

        // ============================================
        // BÁO CÁO & THỐNG KÊ
        // ============================================
        public ActionResult Reports()
        {
            return View();
        }

        // API: Dữ liệu người dùng mới
        public JsonResult GetNewUserData()
        {
            var startDate = DateTime.Now.AddDays(-30);

            var data = _context.Users
                .Where(u => u.CreatedAt >= startDate && !u.IsDeleted)
                .GroupBy(u => DbFunctions.TruncateTime(u.CreatedAt))
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToList();

            var labels = data.Select(d => d.Date.Value.ToString("dd/MM")).ToArray();
            var values = data.Select(d => d.Count).ToArray();

            return Json(new { labels, values }, JsonRequestBehavior.AllowGet);
        }

        // API: Dữ liệu tin nhắn theo ngày
        public JsonResult GetMessageData()
        {
            var startDate = DateTime.Now.AddDays(-30);

            var data = _context.Messages
                .Where(m => m.Timestamp >= startDate)
                .GroupBy(m => DbFunctions.TruncateTime(m.Timestamp))
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToList();

            var labels = data.Select(d => d.Date.Value.ToString("dd/MM")).ToArray();
            var values = data.Select(d => d.Count).ToArray();

            return Json(new { labels, values }, JsonRequestBehavior.AllowGet);
        }

        // API: Top nhóm hoạt động
        public JsonResult GetTopGroups()
        {
            var startDate = DateTime.Now.AddDays(-30);

            var data = _context.Messages
                .Where(m => m.Timestamp >= startDate)
                .GroupBy(m => m.Group.GroupName)
                .Select(g => new { GroupName = g.Key, MessageCount = g.Count() })
                .OrderByDescending(x => x.MessageCount)
                .Take(10)
                .ToList();

            var labels = data.Select(d => d.GroupName).ToArray();
            var values = data.Select(d => d.MessageCount).ToArray();

            return Json(new { labels, values }, JsonRequestBehavior.AllowGet);
        }

        // ============================================
        // HOẠT ĐỘNG NGƯỜI DÙNG
        // ============================================
        public ActionResult UserActivity()
        {
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);

            // User hoạt động gần đây (đã gửi tin nhắn)
            var activeUsers = _context.Messages
                .Where(m => m.Timestamp >= thirtyDaysAgo)
                .GroupBy(m => m.Sender)
                .Select(g => new
                {
                    User = g.Key,
                    MessageCount = g.Count(),
                    LastActivity = g.Max(m => m.Timestamp)
                })
                .OrderByDescending(x => x.MessageCount)
                .Take(50)
                .ToList();

            return View(activeUsers);
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