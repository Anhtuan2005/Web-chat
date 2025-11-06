using Online_chat.Models;
using System;
using System.Data.Entity;
using System.Linq;
using System.Web.Mvc;

namespace Online_chat.Controllers
{
    [Authorize]
    public class AdminController : Controller
    {
        private readonly ApplicationDbContext _context = new ApplicationDbContext();
        public ActionResult Index()
        {
            ViewBag.UserCount = _context.Users.Count(u => u.IsDeleted == false);
            ViewBag.GroupCount = _context.Groups.Count();
            ViewBag.MessageCount = _context.Messages.Count();

            return View();
        }
        public ActionResult ManageUsers()
        {
            var users = _context.Users.Where(u => u.IsDeleted == false).ToList();
            return View(users);
        }
        public ActionResult EditUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null || user.IsDeleted == true) 
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
        public ActionResult DeleteUser(int id)
        {
            var user = _context.Users.Find(id);
            if (user == null)
            {
                return HttpNotFound();
            }
            if (user.Username.ToLower() == User.Identity.Name.ToLower())
            {
                TempData["ErrorMessage"] = "Bạn không thể xóa chính mình!";
                return RedirectToAction("ManageUsers");
            }

            user.IsDeleted = true; 
            _context.Entry(user).State = EntityState.Modified; 

            _context.SaveChanges(); 

            TempData["SuccessMessage"] = $"Đã xóa người dùng: {user.Username}";
            return RedirectToAction("ManageUsers");
        }

        public ActionResult ManageGroups()
        {
            var groups = _context.Groups.Include(g => g.Owner).ToList();
            return View(groups);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteGroup(int id)
        {
            var group = _context.Groups.Find(id);
            if (group == null)
            {
                return HttpNotFound();
            }

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
            if (group == null)
            {
                return HttpNotFound();
            }
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
            return View(group);
        }
        public ActionResult ManageMessages()
        {
            var messages = _context.Messages
                                    .Include(m => m.Sender)
                                    .Include(m => m.Group)
                                    .OrderByDescending(m => m.Timestamp)
                                    .Take(100)
                                    .ToList();

            return View(messages);
        }
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteMessage(int id)
        {
            var message = _context.Messages.Find(id);
            if (message == null)
            {
                return HttpNotFound();
            }

            _context.Messages.Remove(message);
            _context.SaveChanges();
            TempData["SuccessMessage"] = "Đã xóa tin nhắn thành công!";

            return RedirectToAction("ManageMessages");
        }
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
        public JsonResult GetNewUserData()
        {
            var startDate = DateTime.Now.AddDays(-30);

            var data = _context.Users
                .Where(u => u.CreatedAt >= startDate && u.IsDeleted == false) // Sửa lại: Lọc user chưa bị xóa
                .GroupBy(u => DbFunctions.TruncateTime(u.CreatedAt))
                .Select(g => new
                {
                    Date = g.Key,
                    Count = g.Count()
                })
                .OrderBy(x => x.Date)
                .ToList();

            var labels = data.Select(d => d.Date.Value.ToString("dd/MM")).ToArray();
            var values = data.Select(d => d.Count).ToArray();

            return Json(new { labels = labels, values = values }, JsonRequestBehavior.AllowGet);
        }
        public ActionResult Reports()
        {
            return View();
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