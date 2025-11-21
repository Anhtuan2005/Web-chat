using System;
using System.Collections.Generic;
using System.Linq;
using System.Web.Mvc;
using Online_chat.Models;
using Microsoft.AspNet.Identity;
using System.Data.Entity;

namespace Online_chat.Controllers
{
    [Authorize]
    public class GroupController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult Create(string name, List<int> memberIds)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return Json(new { success = false, message = "Tên nhóm không được để trống." });
            }

            if (memberIds == null || memberIds.Count < 2)
            {
                return Json(new { success = false, message = "Nhóm phải có ít nhất 3 thành viên (bao gồm bạn)." });
            }

            var currentUsername = User.Identity.Name;
            var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (currentUser == null)
            {
                return Json(new { success = false, message = "Không tìm thấy người dùng hiện tại." });
            }

            var currentUserId = currentUser.Id;

            // Thêm người tạo vào danh sách nếu chưa có
            if (!memberIds.Contains(currentUserId))
            {
                memberIds.Add(currentUserId);
            }

            try
            {
                // Tạo nhóm
                var group = new Group
                {
                    Name = name,
                    CreatedAt = DateTime.Now,
                    CreatedBy = currentUser.Username,
                    OwnerId = currentUser.Id,
                    AvatarUrl = "/Content/default-avatar.png" // Mặc định
                };

                db.Groups.Add(group);
                db.SaveChanges();

                // Thêm thành viên
                foreach (var memberId in memberIds.Distinct())
                {
                    var role = (memberId == currentUserId) ? "Admin" : "Member";
                    var groupMember = new GroupMember
                    {
                        GroupId = group.Id,
                        UserId = memberId,
                        Role = role,
                        JoinedAt = DateTime.Now
                    };
                    db.GroupMembers.Add(groupMember);
                }

                db.SaveChanges();

                return Json(new
                {
                    success = true,
                    groupId = group.Id,
                    groupName = group.Name,
                    message = "Tạo nhóm thành công!"
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpGet]
        public JsonResult GetUserGroups()
        {
            var currentUsername = User.Identity.Name;
            var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (currentUser == null)
            {
                return Json(new List<ConversationViewModel>(), JsonRequestBehavior.AllowGet);
            }

            var currentUserId = currentUser.Id;

            var groups = db.GroupMembers
                .Where(gm => gm.UserId == currentUserId)
                .Include(gm => gm.Group)
                .Include(gm => gm.Group.Messages)
                .Select(gm => gm.Group)
                .ToList()
                .Select(g => new ConversationViewModel
                {
                    Type = "Group",
                    Id = g.Id.ToString(),
                    Name = g.Name,
                    AvatarUrl = g.AvatarUrl ?? "/Content/default-avatar.png",
                    LastMessage = g.Messages
                        .OrderByDescending(m => m.Timestamp)
                        .Select(m => m.Content)
                        .FirstOrDefault(),
                    LastMessageTimestamp = g.Messages
                        .OrderByDescending(m => m.Timestamp)
                        .Select(m => m.Timestamp)
                        .FirstOrDefault(),
                    UnreadCount = 0
                })
                .OrderByDescending(c => c.LastMessageTimestamp)
                .ToList();

            return Json(groups, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public JsonResult GetGroupInfo(int groupId)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (currentUser == null)
            {
                return Json(new { success = false, message = "Không tìm thấy người dùng" }, JsonRequestBehavior.AllowGet);
            }

            var group = db.Groups.Find(groupId);
            if (group == null)
            {
                return Json(new { success = false, message = "Không tìm thấy nhóm" }, JsonRequestBehavior.AllowGet);
            }

            var members = db.GroupMembers
                .Where(gm => gm.GroupId == groupId)
                .Include(gm => gm.User)
                .Select(gm => new
                {
                    UserId = gm.UserId,
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
                    OwnerId = group.OwnerId
                },
                members = members,
                isOwner = group.OwnerId == currentUser.Id
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult AddMember(int groupId, int userId)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (currentUser == null)
            {
                return Json(new { success = false, message = "Không tìm thấy người dùng" });
            }

            var group = db.Groups.Find(groupId);
            if (group == null)
            {
                return Json(new { success = false, message = "Không tìm thấy nhóm" });
            }

            // Kiểm tra quyền (chỉ admin mới được thêm thành viên)
            var currentMember = db.GroupMembers
                .FirstOrDefault(gm => gm.GroupId == groupId && gm.UserId == currentUser.Id);

            if (currentMember == null || currentMember.Role != "Admin")
            {
                return Json(new { success = false, message = "Bạn không có quyền thêm thành viên" });
            }

            // Kiểm tra xem user đã trong nhóm chưa
            var existingMember = db.GroupMembers
                .FirstOrDefault(gm => gm.GroupId == groupId && gm.UserId == userId);

            if (existingMember != null)
            {
                return Json(new { success = false, message = "Người dùng đã trong nhóm" });
            }

            try
            {
                var newMember = new GroupMember
                {
                    GroupId = groupId,
                    UserId = userId,
                    Role = "Member",
                    JoinedAt = DateTime.Now
                };

                db.GroupMembers.Add(newMember);
                db.SaveChanges();

                return Json(new { success = true, message = "Thêm thành viên thành công" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult RemoveMember(int groupId, int userId)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (currentUser == null)
            {
                return Json(new { success = false, message = "Không tìm thấy người dùng" });
            }

            var group = db.Groups.Find(groupId);
            if (group == null)
            {
                return Json(new { success = false, message = "Không tìm thấy nhóm" });
            }

            // Kiểm tra quyền
            var currentMember = db.GroupMembers
                .FirstOrDefault(gm => gm.GroupId == groupId && gm.UserId == currentUser.Id);

            if (currentMember == null || currentMember.Role != "Admin")
            {
                return Json(new { success = false, message = "Bạn không có quyền xóa thành viên" });
            }

            // Không cho xóa owner
            if (userId == group.OwnerId)
            {
                return Json(new { success = false, message = "Không thể xóa chủ nhóm" });
            }

            try
            {
                var memberToRemove = db.GroupMembers
                    .FirstOrDefault(gm => gm.GroupId == groupId && gm.UserId == userId);

                if (memberToRemove != null)
                {
                    db.GroupMembers.Remove(memberToRemove);
                    db.SaveChanges();
                    return Json(new { success = true, message = "Xóa thành viên thành công" });
                }

                return Json(new { success = false, message = "Không tìm thấy thành viên" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult LeaveGroup(int groupId)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (currentUser == null)
            {
                return Json(new { success = false, message = "Không tìm thấy người dùng" });
            }

            var group = db.Groups.Find(groupId);
            if (group == null)
            {
                return Json(new { success = false, message = "Không tìm thấy nhóm" });
            }

            // Không cho owner rời nhóm
            if (currentUser.Id == group.OwnerId)
            {
                return Json(new { success = false, message = "Chủ nhóm không thể rời nhóm. Hãy chuyển quyền chủ nhóm trước." });
            }

            try
            {
                var membership = db.GroupMembers
                    .FirstOrDefault(gm => gm.GroupId == groupId && gm.UserId == currentUser.Id);

                if (membership != null)
                {
                    db.GroupMembers.Remove(membership);
                    db.SaveChanges();
                    return Json(new { success = true, message = "Rời nhóm thành công" });
                }

                return Json(new { success = false, message = "Bạn không phải thành viên nhóm" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi: " + ex.Message });
            }
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public JsonResult DeleteGroup(int groupId)
        {
            var currentUsername = User.Identity.Name;
            var currentUser = db.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (currentUser == null)
            {
                return Json(new { success = false, message = "User not found." });
            }

            var group = db.Groups.Find(groupId);

            if (group == null)
            {
                return Json(new { success = false, message = "Group not found." });
            }

            // Only the owner can delete the group
            if (group.OwnerId != currentUser.Id)
            {
                return Json(new { success = false, message = "You are not the owner of this group." });
            }

            try
            {
                // Manually delete related entities first to avoid constraint violations
                var members = db.GroupMembers.Where(gm => gm.GroupId == groupId).ToList();
                db.GroupMembers.RemoveRange(members);

                var messages = db.GroupMessages.Where(gm => gm.GroupId == groupId).ToList();
                db.GroupMessages.RemoveRange(messages);

                db.Groups.Remove(group);
                db.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Log the exception
                return Json(new { success = false, message = "An error occurred while deleting the group." });
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