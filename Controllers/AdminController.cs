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
        public JsonResult BanUser(int userId, int days)
        {
            try
            {
                var user = _context.Users.FirstOrDefault(u => u.Id == userId);
                if (user == null) return Json(new { success = false, message = "User not found" });

                user.BanExpiresAt = DateTime.Now.AddDays(days);
                _context.SaveChanges();

                var hubContext = Microsoft.AspNet.SignalR.GlobalHost.ConnectionManager.GetHubContext<Hubs.ChatHub>();
                hubContext.Clients.User(user.Username).forceLogout();

                return Json(new { success = true, message = "Đã khóa tài khoản thành công." });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        [HttpPost]
        public JsonResult UnbanUser(int userId)
        {
            var user = _context.Users.FirstOrDefault(u => u.Id == userId);
            if (user != null)
            {
                user.BanExpiresAt = null;
                _context.SaveChanges();
                return Json(new { success = true, message = "Đã mở khóa tài khoản." });
            }
            return Json(new { success = false, message = "Lỗi." });
        }

        public ActionResult BannedUsers()
        {
            var bannedUsers = _context.Users
                .Where(u => u.BanExpiresAt != null && u.BanExpiresAt > DateTime.Now)
                .OrderByDescending(u => u.BanExpiresAt)
                .ToList();

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
            TempData["SuccessMessage"] = string.Format("Đã xóa nhóm: {0}", group.Name);

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

            TempData["SuccessMessage"] = string.Format("Đã xóa {0} tin nhắn từ {1:dd/MM/yyyy} đến {2:dd/MM/yyyy}", count, fromDate, toDate);
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

            TempData["SuccessMessage"] = string.Format("Đã xóa {0} tin nhắn.", count);
            return RedirectToAction("ManageMessages");
        }

        // Thống kê tin nhắn
        public ActionResult MessageStatistics()
        {
            var stats = new MessageStatisticsViewModel
            {
                TotalGroupMessages = _context.GroupMessages.Count(),
                TotalPrivateMessages = _context.PrivateMessages.Count(),
                TotalUsers = _context.Users.Count(u => !u.IsDeleted),
                TotalGroups = _context.Groups.Count()
            };
            stats.TotalMessages = stats.TotalGroupMessages + stats.TotalPrivateMessages;

            var topGroupSendersQuery = _context.GroupMessages
                .GroupBy(m => m.SenderId)
                .Select(g => new { SenderId = g.Key, MessageCount = g.Count() })
                .OrderByDescending(x => x.MessageCount)
                .Take(10)
                .ToList();

            stats.TopGroupSenders = (from senderStat in topGroupSendersQuery
                                     join user in _context.Users on senderStat.SenderId equals user.Id
                                     select new TopSenderViewModel
                                     {
                                         SenderId = user.Id.ToString(), // ĐÃ SỬA: thêm .Id
                                         SenderUsername = user.Username,
                                         MessageCount = senderStat.MessageCount
                                     }).ToList();

            var topPrivateSendersQuery = _context.PrivateMessages
                .GroupBy(m => m.SenderId)
                .Select(g => new { SenderId = g.Key, MessageCount = g.Count() })
                .OrderByDescending(x => x.MessageCount)
                .Take(10)
                .ToList();

            stats.TopPrivateSenders = (from senderStat in topPrivateSendersQuery
                                       join user in _context.Users on senderStat.SenderId equals user.Id
                                       select new TopSenderViewModel
                                       {
                                           SenderId = user.Id.ToString(), // ĐÃ SỬA: thêm .Id
                                           SenderUsername = user.Username,
                                           MessageCount = senderStat.MessageCount
                                       }).ToList();

            // Top Groups
            var topGroupsQuery = _context.GroupMessages
                .GroupBy(m => m.GroupId)
                .Select(g => new { GroupId = g.Key, MessageCount = g.Count() })
                .OrderByDescending(x => x.MessageCount)
                .Take(10)
                .ToList();

            stats.TopGroups = (from groupStat in topGroupsQuery
                               join grp in _context.Groups on groupStat.GroupId equals grp.Id
                               select new TopGroupViewModel
                               {
                                   GroupId = grp.Id,
                                   GroupName = grp.Name,
                                   MessageCount = groupStat.MessageCount
                               }).ToList();

            var sevenDaysAgo = DateTime.Now.Date.AddDays(-7);

            stats.GroupMessagesLast7Days = _context.GroupMessages
                .Where(m => m.Timestamp >= sevenDaysAgo)
                .GroupBy(m => DbFunctions.TruncateTime(m.Timestamp))
                .Select(g => new DailyMessageCountViewModel
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            stats.PrivateMessagesLast7Days = _context.PrivateMessages
                .Where(m => m.Timestamp >= sevenDaysAgo)
                .GroupBy(m => DbFunctions.TruncateTime(m.Timestamp))
                .Select(g => new DailyMessageCountViewModel
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            return View(stats);
        }


        // Export tin nhắn ra CSV
        public ActionResult ExportMessages(int? groupId, DateTime? fromDate, DateTime? toDate)
        {
            var query = _context.GroupMessages
                .Include(m => m.Sender)
                .Include(m => m.Group)
                .AsQueryable();

            // Lọc theo GroupId
            if (groupId.HasValue)
                query = query.Where(m => m.GroupId == groupId.Value);

            // Lọc theo Từ ngày
            if (fromDate.HasValue)
                query = query.Where(m => m.Timestamp >= fromDate.Value);

            // Lọc theo Đến ngày (Cộng thêm 1 ngày để lấy trọn ngày cuối)
            if (toDate.HasValue)
                query = query.Where(m => m.Timestamp <= toDate.Value.AddDays(1));

            // Lấy dữ liệu
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

            // Tạo nội dung CSV
            var csv = new System.Text.StringBuilder();

            // Header cột
            csv.AppendLine("ID,Group,Sender,Content,Timestamp");

            // Dữ liệu dòng
            foreach (var msg in messages)
            {


                string line = string.Format("{0},\"{1}\",\"{2}\",\"{3}\",{4:yyyy-MM-dd HH:mm:ss}",
                    msg.Id,
                    msg.Group,
                    msg.Sender,
                    msg.Content.Replace("\"", "\"\""),
                    msg.Timestamp);

                csv.AppendLine(line);
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());

            string fileName = string.Format("messages_{0:yyyyMMdd_HHmmss}.csv", DateTime.Now);

            return File(bytes, "text/csv", fileName);
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

            TempData["SuccessMessage"] = string.Format("Đã xóa {0} tin nhắn nhóm và các tin nhắn riêng của user {1}.", count, user.Username);
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
                .GroupBy(m => new
                {
                    User1Id = m.SenderId < m.ReceiverId ? m.SenderId : m.ReceiverId,
                    User2Id = m.SenderId < m.ReceiverId ? m.ReceiverId : m.SenderId
                })
                .Select(g =>
                {
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

            TempData["SuccessMessage"] = string.Format("Đã xóa {0} tin nhắn riêng tư.", count);
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
        public ActionResult ExportPrivateMessages(string search, int? senderId, int? receiverId)
        {
            var query = _context.PrivateMessages
                .Include(m => m.Sender)
                .Include(m => m.Receiver)
                .AsQueryable();

            // Lọc theo người gửi
            if (senderId.HasValue)
                query = query.Where(m => m.SenderId == senderId.Value);

            // Lọc theo người nhận
            if (receiverId.HasValue)
                query = query.Where(m => m.ReceiverId == receiverId.Value);

            // Tìm kiếm nội dung
            if (!string.IsNullOrWhiteSpace(search))
            {
                search = search.ToLower();
                query = query.Where(m => m.Content.ToLower().Contains(search));
            }

            // Lấy dữ liệu
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

            // Tạo nội dung CSV
            var csv = new System.Text.StringBuilder();

            csv.AppendLine("ID,Sender,Receiver,Content,IsRead,Timestamp");

            foreach (var msg in messages)
            {
                string line = string.Format("{0},\"{1}\",\"{2}\",\"{3}\",{4},{5:yyyy-MM-dd HH:mm:ss}",
                    msg.Id,
                    msg.Sender,
                    msg.Receiver,
                    msg.Content.Replace("\"", "\"\""), // Xử lý dấu ngoặc kép trong nội dung
                    msg.IsRead,
                    msg.Timestamp);

                csv.AppendLine(line);
            }

            var bytes = System.Text.Encoding.UTF8.GetBytes(csv.ToString());

            string fileName = string.Format("private_messages_{0:yyyyMMdd_HHmmss}.csv", DateTime.Now);

            return File(bytes, "text/csv", fileName);
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

        public JsonResult GetReportedItemContent(int itemId, string itemType)
        {
            string content = null;
            string message = "";

            try
            {
                if (itemType == "Message")
                {
                    // TÌM GROUP MESSAGE
                    var groupMessage = _context.GroupMessages.FirstOrDefault(m => m.Id == itemId);

                    if (groupMessage != null)
                    {
                        content = groupMessage.Content;
                    }
                    else // TÌM PRIVATE MESSAGE
                    {
                        var privateMessage = _context.PrivateMessages.FirstOrDefault(m => m.Id == itemId);
                        if (privateMessage != null)
                        {
                            content = privateMessage.Content;
                        }
                    }

                    message = content == null ? "Không tìm thấy nội dung tin nhắn." : "Nội dung tin nhắn gốc:";
                }
                else if (itemType == "Post")
                {
                    var post = _context.Posts.FirstOrDefault(p => p.Id == itemId);
                    if (post != null) { content = post.Content; }

                    message = content == null ? "Không tìm thấy nội dung bài viết." : "Nội dung bài viết gốc:";
                }
                else
                {
                    message = "Loại mục không hợp lệ.";
                }

                if (content != null)
                {
                    return Json(new { success = true, content = content, message = message }, JsonRequestBehavior.AllowGet);
                }

                return Json(new { success = false, message = message }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Lỗi GetReportedItemContent: {0}", ex.Message));
                return Json(new { success = false, message = "Lỗi server khi tải nội dung chi tiết." }, JsonRequestBehavior.AllowGet);
            }
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

        public JsonResult GetActivityChartData()
        {
            try
            {
                // Tính mốc 30 ngày trước
                var thirtyDaysAgo = DateTime.Now.Date.AddDays(-30);

                var lineData = _context.GroupMessages
                    .Where(m => m.Timestamp >= thirtyDaysAgo)
                    .ToList() // Kéo dữ liệu về memory trước khi dùng GroupBy/Select phức tạp (An toàn hơn cho C# 5)
                    .GroupBy(m => m.Timestamp.Date) // Group theo ngày
                    .Select(g => new { Date = g.Key, Count = g.Count() })
                    .OrderBy(x => x.Date)
                    .ToList();

                // Chuyển đổi sang Array và dùng string.Format cho nhãn
                var lineLabels = lineData.Select(d => string.Format("{0:dd/MM}", d.Date)).ToArray();
                var lineValues = lineData.Select(d => d.Count).ToArray();

                // 2. Dữ liệu Biểu đồ Tròn (Tỷ lệ Chat Nhóm vs Chat Riêng)
                var totalGroup = _context.GroupMessages.Count();
                var totalPrivate = _context.PrivateMessages.Count();
                var totalMsgs = totalGroup + totalPrivate;

                // Tính tỷ lệ (dùng double để tránh lỗi khi chia)
                double groupRatio = totalMsgs > 0 ? Math.Round((double)totalGroup / totalMsgs * 100, 1) : 0;
                double privateRatio = totalMsgs > 0 ? Math.Round((double)totalPrivate / totalMsgs * 100, 1) : 0;

                return Json(new
                {
                    success = true,
                    lineChart = new
                    {
                        labels = lineLabels,
                        data = lineValues
                    },
                    ratioChart = new
                    {
                        group = groupRatio,
                        @private = privateRatio 
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine(string.Format("Lỗi khi tải dữ liệu biểu đồ: {0}", ex.Message));
                return Json(new { success = false, message = "Lỗi server khi tải dữ liệu." }, JsonRequestBehavior.AllowGet);
            }
        }
    }

}