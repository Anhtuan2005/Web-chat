using System;
using System.Collections.Generic;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using Newtonsoft.Json;
using Online_chat.Models;

namespace Online_chat.Controllers 
{
    [Authorize]
    public class GroupController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();



        public ActionResult Create()
        {
            return View();
        }

        // POST: Group/Create (AJAX)
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Create(HttpPostedFileBase avatar, string groupName, string members)
        {
            try
            {
                var currentUsername = User.Identity.Name;

                var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername);
                if (currentUser == null)
                {
                    return Json(new { success = false, message = "User not found" });
                }

                if (string.IsNullOrWhiteSpace(groupName))
                {
                    return Json(new { success = false, message = "Tên nhóm không được để trống" });
                }

                List<string> memberUsernames;
                try
                {
                    memberUsernames = JsonConvert.DeserializeObject<List<string>>(members);
                }
                catch
                {
                    return Json(new { success = false, message = "Dữ liệu thành viên không hợp lệ" });
                }

                if (memberUsernames == null || memberUsernames.Count == 0)
                {
                    return Json(new { success = false, message = "Vui lòng chọn ít nhất 1 thành viên" });
                }

                string avatarUrl = "/Content/default-group-avatar.png";
                if (avatar != null && avatar.ContentLength > 0)
                {
                    var fileName = $"group_{Guid.NewGuid()}{Path.GetExtension(avatar.FileName)}";
                    var path = Path.Combine(Server.MapPath("~/Uploads/GroupAvatars"), fileName);
                    Directory.CreateDirectory(Path.GetDirectoryName(path));
                    avatar.SaveAs(path);
                    avatarUrl = $"/Uploads/GroupAvatars/{fileName}";
                }

                var group = new Group
                {
                    Name = groupName,
                    AvatarUrl = avatarUrl,
                    CreatedBy = currentUsername,
                    CreatedAt = DateTime.Now,
                    OwnerId = currentUser.Id 
                };

                db.Groups.Add(group);
                db.SaveChanges();

                // Tìm User object của các thành viên
                var memberUsers = db.Users.Where(u => memberUsernames.Contains(u.Username)).ToList();

                var creatorMember = new GroupMember
                {
                    GroupId = group.Id,
                    UserId = currentUser.Id, // Sửa: Dùng UserId (int)
                    Role = "Admin",
                    JoinedAt = DateTime.Now
                };
                db.GroupMembers.Add(creatorMember);

                foreach (var memberUser in memberUsers)
                {
                    if (memberUser.Id == currentUser.Id) continue; // Bỏ qua người tạo

                    var member = new GroupMember
                    {
                        GroupId = group.Id,
                        UserId = currentUser.Id,  
                        Role = "Admin",
                        JoinedAt = DateTime.Now
                    };
                    db.GroupMembers.Add(member);
                }

                db.SaveChanges();

                return Json(new { success = true, groupId = group.Id, message = "Tạo nhóm thành công!" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = $"Lỗi: {ex.Message}" });
            }
        }

        // GET: Group/GetUserGroups
        [HttpGet]
        public ActionResult GetUserGroups()
        {
            try
            {
                var username = User.Identity.Name;

                var groups = db.GroupMembers
                    .Where(gm => gm.User.Username == username)
                    .Select(gm => new
                    {
                        Id = gm.Group.Id,
                        Name = gm.Group.Name,
                        AvatarUrl = gm.Group.AvatarUrl,
                        MemberCount = gm.Group.Members.Count,
                        LastMessage = db.GroupMessages
                            .Where(m => m.GroupId == gm.Group.Id)
                            .OrderByDescending(m => m.Timestamp)
                            .Select(m => new
                            {
                                Content = m.Content,
                                Timestamp = m.Timestamp,
                                SenderUsername = m.Sender.Username 
                            })
                            .FirstOrDefault()
                    })
                    .OrderByDescending(g => g.LastMessage != null ? g.LastMessage.Timestamp : DateTime.MinValue)
                    .ToList();

                return Json(groups, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // GET: Group/GetGroupInfo
        [HttpGet]
        public ActionResult GetGroupInfo(int groupId)
        {
            try
            {
                var username = User.Identity.Name;

                var isMember = db.GroupMembers.Any(gm => gm.GroupId == groupId && gm.User.Username == username);
                if (!isMember)
                {
                    return Json(new { success = false, message = "Bạn không phải thành viên của nhóm này" }, JsonRequestBehavior.AllowGet);
                }

                var group = db.Groups.Find(groupId);
                if (group == null)
                {
                    return Json(new { success = false, message = "Nhóm không tồn tại" }, JsonRequestBehavior.AllowGet);
                }

                var members = db.GroupMembers
                    .Where(gm => gm.GroupId == groupId)
                    .Select(gm => new
                    {
                        Username = gm.User.Username,
                        DisplayName = gm.User.DisplayName,
                        AvatarUrl = gm.User.AvatarUrl,
                        Role = gm.Role,
                        JoinedAt = gm.JoinedAt
                    })
                    .ToList();

                return Json(new
                {
                    success = true,
                    group = new
                    {
                        Id = group.Id,
                        Name = group.Name,
                        AvatarUrl = group.AvatarUrl,
                        CreatedBy = group.CreatedBy,
                        CreatedAt = group.CreatedAt,
                        Members = members
                    }
                }, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message }, JsonRequestBehavior.AllowGet);
            }
        }

        // POST: Group/AddMember
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddMember(int groupId, string username)
        {
            try
            {
                var currentUsername = User.Identity.Name;
                var userToAdd = db.Users.FirstOrDefault(u => u.Username == username);
                if (userToAdd == null)
                {
                    return Json(new { success = false, message = "Người dùng không tồn tại" });
                }

                var isAdmin = db.GroupMembers.Any(gm =>
                    gm.GroupId == groupId &&
                    gm.User.Username == currentUsername &&
                    gm.Role == "Admin");

                if (!isAdmin)
                {
                    return Json(new { success = false, message = "Bạn không có quyền thêm thành viên" });
                }

                var isAlreadyMember = db.GroupMembers.Any(gm =>
                    gm.GroupId == groupId &&
                    gm.UserId == userToAdd.Id);

                if (isAlreadyMember)
                {
                    return Json(new { success = false, message = "Người này đã là thành viên" });
                }

                var member = new GroupMember
                {
                    GroupId = groupId,
                    UserId = userToAdd.Id,
                    Role = "Member",
                    JoinedAt = DateTime.Now
                };
                db.GroupMembers.Add(member);
                db.SaveChanges();

                return Json(new { success = true, message = "Đã thêm thành viên" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Group/RemoveMember
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult RemoveMember(int groupId, string username)
        {
            try
            {
                var currentUsername = User.Identity.Name;
                var userToRemove = db.Users.FirstOrDefault(u => u.Username == username);
                if (userToRemove == null)
                {
                    return Json(new { success = false, message = "Người dùng không tồn tại" });
                }

                var isAdmin = db.GroupMembers.Any(gm =>
                    gm.GroupId == groupId &&
                    gm.User.Username == currentUsername &&
                    gm.Role == "Admin");

                if (!isAdmin)
                {
                    return Json(new { success = false, message = "Bạn không có quyền xóa thành viên" });
                }

                var member = db.GroupMembers.FirstOrDefault(gm =>
                    gm.GroupId == groupId &&
                    gm.UserId == userToRemove.Id);

                if (member == null)
                {
                    return Json(new { success = false, message = "Không tìm thấy thành viên" });
                }

                db.GroupMembers.Remove(member);
                db.SaveChanges();

                return Json(new { success = true, message = "Đã xóa thành viên" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        // POST: Group/Leave
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult Leave(int groupId)
        {
            try
            {
                var username = User.Identity.Name;

                var member = db.GroupMembers.FirstOrDefault(gm =>
                    gm.GroupId == groupId &&
                    gm.User.Username == username);

                if (member == null)
                {
                    return Json(new { success = false, message = "Bạn không phải thành viên của nhóm này" });
                }

                db.GroupMembers.Remove(member);

                var group = db.Groups.Find(groupId);
                if (member.Role == "Admin")
                {
                    var remainingMembers = db.GroupMembers.Where(gm => gm.GroupId == groupId).ToList();
                    if (remainingMembers.Count > 0 && !remainingMembers.Any(m => m.Role == "Admin"))
                    {// Đôn người cũ nhất lên làm Admin
                    }
                }

                db.SaveChanges();

                return Json(new { success = true, message = "Đã rời nhóm" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = ex.Message });
            }
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }
    }
}