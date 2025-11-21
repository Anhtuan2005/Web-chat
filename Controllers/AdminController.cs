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
        // DASHBOARD
        // ============================================
        public ActionResult Index()
        {
            ViewBag.UserCount = _context.Users.Count(u => !u.IsDeleted);
            ViewBag.GroupCount = _context.Groups.Count();
            ViewBag.MessageCount = _context.GroupMessages.Count();
            ViewBag.PrivateMessageCount = _context.PrivateMessages.Count();

            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            ViewBag.NewUsersLast30Days = _context.Users.Count(u => u.CreatedAt >= thirtyDaysAgo && !u.IsDeleted);
            ViewBag.MessagesLast30Days = _context.GroupMessages.Count(m => m.Timestamp >= thirtyDaysAgo);

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

        public ActionResult ManageGroups(string search)
        {
            var query = _context.Groups.Include(g => g.Owner).AsQueryable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(g => g.Name.ToLower().Contains(search));
                ViewBag.SearchQuery = search;
            }

            var groups = query.OrderByDescending(g => g.CreatedAt).ToList();
            return View(groups);
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

        public ActionResult BannedUsers()
        {
            var bannedUsers = _context.Users.Where(u => u.IsDeleted).ToList();
            return View(bannedUsers);
        }

        // ============================================
        // QUẢN LÝ NHÓM
        // ============================================

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteGroup(int id)
        {
            var group = _context.Groups.Find(id);
            if (group == null) return HttpNotFound();

            var messagesInGroup = _context.GroupMessages.Where(m => m.GroupId == id);
            _context.GroupMessages.RemoveRange(messagesInGroup);

            var membersInGroup = _context.GroupMembers.Where(m => m.GroupId == id);
            _context.GroupMembers.RemoveRange(membersInGroup);

            _context.Groups.Remove(group);
            _context.SaveChanges();
            TempData["SuccessMessage"] = $"Đã xóa nhóm: {group.Name}";

            return RedirectToAction("ManageGroups");
        }

        public ActionResult EditGroup(int id)
        {
            var group = _context.Groups.Find(id);
            if (group == null) return HttpNotFound();

            ViewBag.Users = _context.Users
                .Where(u => !u.IsDeleted)
                .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.DisplayName })
                .ToList();

            return View(group);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult EditGroup([Bind(Include = "Id,Name,CreatedAt,CreatedBy,OwnerId")] Group group)
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

        public ActionResult GroupMembers(int id)
        {
            var group = _context.Groups.Find(id);
            if (group == null) return HttpNotFound();

            ViewBag.GroupName = group.Name;
            ViewBag.GroupId = id;

            var members = _context.GroupMembers
                .Where(gm => gm.GroupId == id)
                .Select(gm => gm.User)
                .Distinct()
                .ToList();

            return View(members);
        }

        // ============================================
        // QUẢN LÝ TIN NHẮN NHÓM
        // ============================================
        public ActionResult MessageDetail(int id)
        {
            var message = _context.GroupMessages
                .Include(m => m.Sender)
                .Include(m => m.Group)
                .FirstOrDefault(m => m.Id == id);

            if (message == null) return HttpNotFound();

            return View(message);
        }

        // Lọc theo người gửi
        public ActionResult ManageMessages(string search, int? groupId, int? senderId, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.GroupMessages
                .Include(m => m.Sender)
                .Include(m => m.Group)
                .AsQueryable();

            // Lọc theo nhóm
            if (groupId.HasValue)
            {
                query = query.Where(m => m.GroupId == groupId.Value);
                ViewBag.SelectedGroupId = groupId.Value;
            }

            // Lọc theo người gửi
            if (senderId.HasValue)
            {
                query = query.Where(m => m.SenderId == senderId.Value);
                ViewBag.SelectedSenderId = senderId.Value;
            }

            // Lọc theo thời gian
            if (fromDate.HasValue)
            {
                query = query.Where(m => m.Timestamp >= fromDate.Value);
                ViewBag.FromDate = fromDate.Value.ToString("yyyy-MM-dd");
            }

            if (toDate.HasValue)
            {
                var endDate = toDate.Value.AddDays(1);
                query = query.Where(m => m.Timestamp < endDate);
                ViewBag.ToDate = toDate.Value.ToString("yyyy-MM-dd");
            }

            // Tìm kiếm nội dung
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
                .Select(g => new SelectListItem { Value = g.Id.ToString(), Text = g.Name })
                .ToList();

            ViewBag.Users = _context.Users
                .Where(u => !u.IsDeleted)
                .OrderBy(u => u.Username)
                .Select(u => new SelectListItem { Value = u.Id.ToString(), Text = u.Username })
                .ToList();

            return View(messages);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMessagesByDateRange(DateTime fromDate, DateTime toDate)
        {
            var endDate = toDate.AddDays(1);
            var messages = _context.GroupMessages
                .Where(m => m.Timestamp >= fromDate && m.Timestamp < endDate);

            var count = messages.Count();
            _context.GroupMessages.RemoveRange(messages);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã xóa {count} tin nhắn từ {fromDate:dd/MM/yyyy} đến {toDate:dd/MM/yyyy}";
            return RedirectToAction("ManageMessages");
        }

        // Xóa hàng loạt tin nhắn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMultipleMessages(int[] messageIds)
        {
            if (messageIds == null || messageIds.Length == 0)
            {
                TempData["ErrorMessage"] = "Chưa chọn tin nhắn nào để xóa.";
                return RedirectToAction("ManageMessages");
            }

            var messages = _context.GroupMessages.Where(m => messageIds.Contains(m.Id));
            var count = messages.Count();

            _context.GroupMessages.RemoveRange(messages);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã xóa {count} tin nhắn.";
            return RedirectToAction("ManageMessages");
        }

        // Thống kê tin nhắn
        public ActionResult MessageStatistics()
        {
            var stats = new
            {
                TotalMessages = _context.GroupMessages.Count(),
                TotalUsers = _context.Users.Count(u => !u.IsDeleted),
                TotalGroups = _context.Groups.Count(),

                TopSenders = _context.GroupMessages
                    .GroupBy(m => m.SenderId) 
                    .Select(g => new
                    {
                        SenderId = g.Key,
                        SenderUsername = g.FirstOrDefault().Sender.Username, //  Lấy username
                        MessageCount = g.Count()
                    })
                    .OrderByDescending(x => x.MessageCount)
                    .Take(10)
                    .ToList(),

                // Top 10 nhóm có nhiều tin nhắn nhất
                TopGroups = _context.GroupMessages
                    .GroupBy(m => m.GroupId) // Group by GroupId (int)
                    .Select(g => new
                    {
                        GroupId = g.Key,
                        GroupName = g.FirstOrDefault().Group.Name, // Lấy group name
                        MessageCount = g.Count()
                    })
                    .OrderByDescending(x => x.MessageCount)
                    .Take(10)
                    .ToList(),

                // Tin nhắn trong 7 ngày gần nhất
                MessagesLast7Days = _context.GroupMessages
                    .Where(m => m.Timestamp >= DbFunctions.AddDays(DateTime.Now, -7))
                    .GroupBy(m => DbFunctions.TruncateTime(m.Timestamp))
                    .Select(g => new
                    {
                        Date = g.Key,
                        Count = g.Count()
                    })
                    .OrderBy(x => x.Date)
                    .ToList()
            };

            return View(stats);
        }

        // Tìm tin nhắn có từ khóa nhạy cảm
        public ActionResult SensitiveMessages(string keyword)
        {
            if (string.IsNullOrWhiteSpace(keyword))
            {
                keyword = "spam|fuck|shit|bitch"; // Từ khóa mặc định
            }

            ViewBag.Keyword = keyword;

            var keywords = keyword.ToLower().Split('|');

            var messages = _context.GroupMessages
                .Include(m => m.Sender)
                .Include(m => m.Group)
                .AsEnumerable()
                .Where(m => keywords.Any(k => m.Content.ToLower().Contains(k)))
                .OrderByDescending(m => m.Timestamp)
                .Take(100)
                .ToList();

            return View(messages);
        }

        // Export tin nhắn ra CSV
        public ActionResult ExportMessages(int? groupId, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.GroupMessages
                .Include(m => m.Sender)
                .Include(m => m.Group)
                .AsQueryable();

            if (groupId.HasValue)
                query = query.Where(m => m.GroupId == groupId.Value);

            if (fromDate.HasValue)
                query = query.Where(m => m.Timestamp >= fromDate.Value);

            if (toDate.HasValue)
                query = query.Where(m => m.Timestamp <= toDate.Value.AddDays(1));

            var messages = query
                .OrderBy(m => m.Timestamp)
                .Select(m => new
                {
                    m.Id,
                    Group = m.Group.Name,
                    Sender = m.Sender.Username,
                    Content = m.Content,
                    Timestamp = m.Timestamp
                })
                .ToList();

            // Tạo CSV
            var csv = new System.Text.StringBuilder();
            csv.AppendLine("ID,Group,Sender,Content,Timestamp");

            foreach (var msg in messages)
            {
                csv.AppendLine($"{msg.Id},\"{msg.Group}\",\"{msg.Sender}\",\"{msg.Content.Replace("\"", "\"\"")}\",{msg.Timestamp:yyyy-MM-dd HH:mm:ss}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"messages_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMessage(int id)
        {
            var message = _context.GroupMessages.Find(id);
            if (message == null) return HttpNotFound();

            _context.GroupMessages.Remove(message);
            _context.SaveChanges();
            TempData["SuccessMessage"] = "Đã xóa tin nhắn thành công!";

            return RedirectToAction("ManageMessages");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteUserMessages(string userId) 
        {
            var user = _context.Users.Find(userId); 
            if (user == null)
            {
                TempData["ErrorMessage"] = "Không tìm thấy user.";
                return RedirectToAction("ManageMessages");
            }

            var messages = _context.GroupMessages.Where(m => m.SenderId == int.Parse(userId));
            var count = messages.Count();

            _context.GroupMessages.RemoveRange(messages);

            var privateMessages = _context.PrivateMessages
                .Where(m => m.SenderId == int.Parse(userId) || m.ReceiverId == int.Parse(userId));

            _context.PrivateMessages.RemoveRange(privateMessages);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã xóa {count} tin nhắn nhóm và các tin nhắn riêng của user {user.Username}.";
            return RedirectToAction("ManageMessages");
        }

        // ============================================
        // QUẢN LÝ TIN NHẮN RIÊNG TƯ
        // ============================================
        public ActionResult ManagePrivateMessages(string search)
        {
            IQueryable<PrivateMessage> query = _context.PrivateMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver);

            var conversations = query
                .ToList() // Fetch all messages to group in memory
                .GroupBy(m => new {
                    User1Id = m.SenderId < m.ReceiverId ? m.SenderId : m.ReceiverId,
                    User2Id = m.SenderId < m.ReceiverId ? m.ReceiverId : m.SenderId
                })
                .Select(g => {
                    var lastMessage = g.OrderByDescending(m => m.Timestamp).First();
                    return new AdminConversationViewModel
                    {
                        User1 = lastMessage.SenderId == g.Key.User1Id ? lastMessage.Sender : lastMessage.Receiver,
                        User2 = lastMessage.SenderId == g.Key.User2Id ? lastMessage.Sender : lastMessage.Receiver,
                        MessageCount = g.Count(),
                        LastMessageTimestamp = lastMessage.Timestamp,
                        LastMessageContent = lastMessage.Content
                    };
                })
                .AsEnumerable();

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                conversations = conversations.Where(c =>
                    c.User1.Username.ToLower().Contains(search) ||
                    c.User2.Username.ToLower().Contains(search) ||
                    c.User1.DisplayName.ToLower().Contains(search) ||
                    c.User2.DisplayName.ToLower().Contains(search)
                );
                ViewBag.SearchQuery = search;
            }

            var orderedConversations = conversations
                .OrderByDescending(c => c.LastMessageTimestamp)
                .ToList();

            return View(orderedConversations);
        }

        public ActionResult ConversationDetails(int user1Id, int user2Id)
        {
            var user1 = _context.Users.Find(user1Id);
            var user2 = _context.Users.Find(user2Id);

            if (user1 == null || user2 == null)
            {
                return HttpNotFound();
            }

            ViewBag.User1 = user1;
            ViewBag.User2 = user2;

            var messages = _context.PrivateMessages
                .Include(m => m.Sender)
                .Where(m =>
                    (m.SenderId == user1Id && m.ReceiverId == user2Id) ||
                    (m.SenderId == user2Id && m.ReceiverId == user1Id)
                )
                .OrderBy(m => m.Timestamp)
                .ToList();

            return View("ConversationDetails", messages);
        }

        // Xóa nhiều tin nhắn
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMultiplePrivateMessages(int[] messageIds)
        {
            if (messageIds == null || messageIds.Length == 0)
            {
                TempData["ErrorMessage"] = "Chưa chọn tin nhắn nào để xóa.";
                return RedirectToAction("ManagePrivateMessages");
            }

            var messages = _context.PrivateMessages.Where(m => messageIds.Contains(m.Id));
            var count = messages.Count();

            _context.PrivateMessages.RemoveRange(messages);
            _context.SaveChanges();

            TempData["SuccessMessage"] = $"Đã xóa {count} tin nhắn riêng tư.";
            return RedirectToAction("ManagePrivateMessages");
        }

        // Chi tiết tin nhắn riêng tư
        public ActionResult PrivateMessageDetail(int id)
        {
            var message = _context.PrivateMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .FirstOrDefault(m => m.Id == id);

            if (message == null) return HttpNotFound();

            return View(message);
        }

        // Export CSV
        public ActionResult ExportPrivateMessages(string search, int? senderId, int? receiverId)
        {
            var query = _context.PrivateMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .AsQueryable();

            if (senderId.HasValue)
                query = query.Where(m => m.SenderId == senderId.Value);

            if (receiverId.HasValue)
                query = query.Where(m => m.ReceiverId == receiverId.Value);

            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(m => m.Content.ToLower().Contains(search));
            }

            var messages = query
                .OrderBy(m => m.Timestamp)
                .Select(m => new
                {
                    m.Id,
                    Sender = m.Sender.Username,
                    Receiver = m.Receiver.Username,
                    Content = m.Content,
                    IsRead = m.IsRead,
                    Timestamp = m.Timestamp
                })
                .ToList();

            var csv = new System.Text.StringBuilder();
            csv.AppendLine("ID,Sender,Receiver,Content,IsRead,Timestamp");

            foreach (var msg in messages)
            {
                csv.AppendLine($"{msg.Id},\"{msg.Sender}\",\"{msg.Receiver}\",\"{msg.Content.Replace("\"", "\"\"")}\",{msg.IsRead},{msg.Timestamp:yyyy-MM-dd HH:mm:ss}");
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());
            return File(bytes, "text/csv", $"private_messages_{DateTime.Now:yyyyMMdd_HHmmss}.csv");
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
            var reports = _context.Reports
                .Include(r => r.Reporter)
                .Include(r => r.ReportedUser)
                .OrderByDescending(r => r.CreatedAt)
                .ToList();
            return View(reports);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ResolveReport(int id)
        {
            var report = _context.Reports.Find(id);
            if (report == null)
            {
                return HttpNotFound();
            }

            report.IsResolved = true;
            _context.Entry(report).State = EntityState.Modified;
            _context.SaveChanges();

            TempData["SuccessMessage"] = "Đã giải quyết báo cáo.";
            return RedirectToAction("Reports");
        }

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

        public JsonResult GetMessageData()
        {
            var startDate = DateTime.Now.AddDays(-30);
            var data = _context.GroupMessages
                .Where(m => m.Timestamp >= startDate)
                .GroupBy(m => DbFunctions.TruncateTime(m.Timestamp))
                .Select(g => new { Date = g.Key, Count = g.Count() })
                .OrderBy(x => x.Date)
                .ToList();
            var labels = data.Select(d => d.Date.Value.ToString("dd/MM")).ToArray();
            var values = data.Select(d => d.Count).ToArray();
            return Json(new { labels, values }, JsonRequestBehavior.AllowGet);
        }

        public JsonResult GetTopGroups()
        {
            var startDate = DateTime.Now.AddDays(-30);
            var data = _context.GroupMessages
                .Where(m => m.Timestamp >= startDate)
                .GroupBy(m => m.Group.Name)
                .Select(g => new { GroupName = g.Key, MessageCount = g.Count() })
                .OrderByDescending(x => x.MessageCount)
                .Take(10)
                .ToList();
            var labels = data.Select(d => d.GroupName).ToArray();
            var values = data.Select(d => d.MessageCount).ToArray();
            return Json(new { labels, values }, JsonRequestBehavior.AllowGet);
        }

        public ActionResult UserActivity()
        {
            var thirtyDaysAgo = DateTime.Now.AddDays(-30);
            var activeUsers = _context.GroupMessages
                .Where(m => m.Timestamp >= thirtyDaysAgo)
                .GroupBy(m => m.Sender)
                .Select(g => new UserActivityViewModel
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

        [ChildActionOnly]
        public ActionResult _AdminHeaderNotifications()
        {
            var model = new NotificationViewModel();
            if (User.Identity.IsAuthenticated)
            {
                model.RecentUnreadMessages = _context.PrivateMessages
                    .Include(m => m.Sender)
                    .OrderByDescending(m => m.Timestamp)
                    .Where(m => !m.IsRead)
                    .Take(5)
                    .ToList();
                model.UnreadMessagesCount = _context.PrivateMessages.Count(m => !m.IsRead);

                model.RecentUnresolvedReports = _context.Reports
                    .Include(r => r.Reporter)
                    .Include(r => r.ReportedUser)
                    .OrderByDescending(r => r.CreatedAt)
                    .Where(r => !r.IsResolved)
                    .Take(5)
                    .ToList();
                model.UnresolvedReportsCount = _context.Reports.Count(r => !r.IsResolved);
            }
            return PartialView("~/Views/Shared/_AdminHeaderNotifications.cshtml", model);
        }

        
    }
}