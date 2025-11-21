using Online_chat.Models;
using System.Web.Mvc;
using System.Linq;
using System.Data.Entity;
using Microsoft.AspNet.Identity; // Added for GetUserId
using System.Web; // Added for HttpPostedFileBase
using System.IO; // Added for Path operations
using System; // Added for Guid

namespace Online_chat.Controllers
{
    [Authorize]
    public class FeedController : Controller
    {
        private ApplicationDbContext db = new ApplicationDbContext();

        // GET: Feed
        public ActionResult Index()
        {
            var username = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);
            ViewBag.CurrentUserId = user?.Id ?? 0;
            ViewBag.CurrentUserAvatar = user?.AvatarUrl ?? "/Content/default-avatar.png";

            var posts = db.Posts
                .Include(p => p.User)
                .Include(p => p.Likes)
                .Include(p => p.Comments.Select(c => c.User)) // Eager load comments and their users
                .OrderByDescending(p => p.CreatedAt)
                .Take(10) // Take the first page of posts
                .ToList();
            return View(posts);
        }

        public ActionResult GetPosts(int page = 2)
        {
            var username = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);
            ViewBag.CurrentUserId = user?.Id ?? 0;
            ViewBag.CurrentUserAvatar = user?.AvatarUrl ?? "/Content/default-avatar.png";

            int pageSize = 10;
            var posts = db.Posts
                .Include(p => p.User)
                .Include(p => p.Likes)
                .Include(p => p.Comments.Select(c => c.User))
                .Where(p => p.PostType == "post") // Only get regular posts
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            if (!posts.Any())
            {
                return Content(""); // No more posts
            }

            return PartialView("_PostListPartial", posts);
        }

        public ActionResult GetStories()
        {
            try
            {
                var stories = db.Posts
                    .Include(p => p.User)
                    .Where(p => p.PostType == "story")
                    .OrderByDescending(p => p.CreatedAt)
                    .ToList();

                return PartialView("_StoryListPartial", stories);
            }
            catch (Exception ex)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.InternalServerError, "Lỗi khi tải tin: " + ex.Message);
            }
        }

        // GET: Feed/Create
        public ActionResult Create()
        {
            return View();
        }

        // POST: Feed/Create
        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreatePost()
        {
            try
            {
                string username = User.Identity.Name;
                var currentUser = db.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);

                if (currentUser == null)
                {
                    return Json(new { success = false, message = "Không thể xác thực người dùng. Vui lòng đăng nhập lại." });
                }

                var post = new Post
                {
                    UserId = currentUser.Id,
                    CreatedAt = DateTime.Now,
                    Privacy = Request.Form["privacy"] ?? "Public",
                    PostType = Request.Form["postType"] ?? "post"
                };

                if (post.PostType == "story")
                {
                    post.Content = Request.Form["storyContent"];
                    post.MediaUrl = Request.Form["storyBackground"];
                    post.MediaType = "StoryBackground";
                }
                else if (post.PostType == "event")
                {
                    var eventName = Request.Form["eventName"];
                    var eventLocation = Request.Form["eventLocation"];
                    var eventStartDate = Request.Form["eventStartDate"];
                    var eventEndDate = Request.Form["eventEndDate"];
                    var eventDescription = Request.Form["eventDescription"];

                    post.Content = $"Sự kiện: {eventName}\n" +
                                   $"Địa điểm: {eventLocation}\n" +
                                   $"Bắt đầu: {eventStartDate}\n" +
                                   $"Kết thúc: {eventEndDate}\n\n" +
                                   $"{eventDescription}";
                }
                else if (post.PostType == "feeling")
                {
                    post.Content = Request.Form["feelingContent"];
                }
                else // Default to "post"
                {
                    post.Content = Request.Form["content"];
                }

                HttpPostedFileBase mediaFile = Request.Files.Count > 0 ? Request.Files[0] : null;

                if (mediaFile != null && mediaFile.ContentLength > 0)
                {
                    var fileName = Path.GetFileName(mediaFile.FileName);
                    var fileExtension = Path.GetExtension(fileName).ToLower();
                    var uniqueFileName = Guid.NewGuid().ToString() + fileExtension;
                    var path = Path.Combine(Server.MapPath("~/Uploads/Posts/"), uniqueFileName);

                    Directory.CreateDirectory(Server.MapPath("~/Uploads/Posts/"));
                    mediaFile.SaveAs(path);

                    post.MediaUrl = "/Uploads/Posts/" + uniqueFileName;

                    if (fileExtension == ".jpg" || fileExtension == ".jpeg" || fileExtension == ".png" || fileExtension == ".gif")
                    {
                        post.MediaType = "Image";
                    }
                    else if (fileExtension == ".mp4" || fileExtension == ".webm" || fileExtension == ".ogg")
                    {
                        post.MediaType = "Video";
                    }
                    else
                    {
                        return Json(new { success = false, message = "Loại tệp không được hỗ trợ." });
                    }
                }
                else if (string.IsNullOrWhiteSpace(post.Content) && string.IsNullOrWhiteSpace(post.MediaUrl))
                {
                    return Json(new { success = false, message = "Bài đăng phải có nội dung hoặc tệp phương tiện." });
                }

                db.Posts.Add(post);
                db.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Log the exception
                return Json(new { success = false, message = "Đã xảy ra lỗi không mong muốn." });
            }
        }

        public ActionResult Events()
        {
            return View();
        }
        public ActionResult SelectStoryType()
        {
            return View();
        }

        public ActionResult CreateStory(string type)
        {
            ViewBag.StoryType = type ?? "text";
            return View();
        }

        protected override void Dispose(bool disposing)
        {
            if (disposing)
            {
                db.Dispose();
            }
            base.Dispose(disposing);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ToggleLike(int postId)
        {
            var username = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);

            if (user == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Unauthorized, "Vui lòng đăng nhập lại.");
            }

            var existingLike = db.Likes.FirstOrDefault(l => l.PostId == postId && l.UserId == user.Id);

            if (existingLike != null)
            {
                // User has already liked the post, so unlike it
                db.Likes.Remove(existingLike);
            }
            else
            {
                // User has not liked the post, so like it
                var newLike = new Like
                {
                    PostId = postId,
                    UserId = user.Id,
                    CreatedAt = DateTime.Now
                };
                db.Likes.Add(newLike);
            }

            db.SaveChanges();

            var likeCount = db.Likes.Count(l => l.PostId == postId);
            var userHasLiked = db.Likes.Any(l => l.PostId == postId && l.UserId == user.Id);

            return Json(new { success = true, likeCount = likeCount, userHasLiked = userHasLiked });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult AddComment(int postId, string content)
        {
            var username = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);

            if (user == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Unauthorized, "Vui lòng đăng nhập lại.");
            }

            if (string.IsNullOrWhiteSpace(content))
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Nội dung bình luận không được để trống.");
            }

            var comment = new Comment
            {
                PostId = postId,
                UserId = user.Id,
                Content = content,
                CreatedAt = DateTime.Now
            };

            db.Comments.Add(comment);
            db.SaveChanges();

            // We need to pass the full comment object with the user to the partial view
            var newComment = db.Comments.Include(c => c.User).FirstOrDefault(c => c.Id == comment.Id);

            return PartialView("_CommentPartial", newComment);
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeleteComment(int commentId)
        {
            var username = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);

            if (user == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Unauthorized, "Vui lòng đăng nhập lại.");
            }

            var comment = db.Comments.FirstOrDefault(c => c.Id == commentId);

            if (comment == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "Không tìm thấy bình luận.");
            }

            if (comment.UserId != user.Id)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "Bạn không có quyền xóa bình luận này.");
            }

            db.Comments.Remove(comment);
            db.SaveChanges();

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult DeletePost(int postId)
        {
            var username = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);

            if (user == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Unauthorized, "Vui lòng đăng nhập lại.");
            }

            var post = db.Posts.FirstOrDefault(p => p.Id == postId);

            if (post == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "Không tìm thấy bài viết.");
            }

            if (post.UserId != user.Id)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "Bạn không có quyền xóa bài viết này.");
            }

            // Likes and Comments will be cascade deleted because of the model configuration
            db.Posts.Remove(post);
            db.SaveChanges();

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult SetPostPrivacy(int postId, string privacy)
        {
            var username = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);

            if (user == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Unauthorized, "Vui lòng đăng nhập lại.");
            }

            var post = db.Posts.FirstOrDefault(p => p.Id == postId);

            if (post == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "Không tìm thấy bài viết.");
            }

            if (post.UserId != user.Id)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "Bạn không có quyền thay đổi cài đặt này.");
            }

            if (privacy == "Public" || privacy == "Private")
            {
                post.Privacy = privacy;
                db.SaveChanges();
                return Json(new { success = true, newPrivacy = post.Privacy });
            }

            return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Cài đặt quyền riêng tư không hợp lệ.");
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult ReportPost(int postId, string reason)
        {
            var username = User.Identity.Name;
            var reporter = db.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);

            if (reporter == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Unauthorized, "Vui lòng đăng nhập lại.");
            }

            var post = db.Posts.Include(p => p.User).FirstOrDefault(p => p.Id == postId);

            if (post == null)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.NotFound, "Không tìm thấy bài viết.");
            }

            if (post.UserId == reporter.Id)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.BadRequest, "Bạn không thể báo cáo bài viết của chính mình.");
            }

            var report = new Report
            {
                ReporterId = reporter.Id,
                ReportedUserId = post.UserId,
                PostId = postId,
                Reason = reason,
                CreatedAt = DateTime.Now,
                IsResolved = false
            };

            db.Reports.Add(report);
            db.SaveChanges();

            return Json(new { success = true });
        }

        [HttpPost]
        [ValidateAntiForgeryToken]
        public ActionResult CreateStory(FormCollection form)
        {
            try
            {
                string username = User.Identity.Name;
                var currentUser = db.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);

                if (currentUser == null)
                {
                    return Json(new { success = false, message = "Không thể xác thực người dùng. Vui lòng đăng nhập lại." });
                }

                string storyType = form["storyType"];
                var post = new Post
                {
                    UserId = currentUser.Id,
                    CreatedAt = DateTime.Now,
                    Privacy = "Public", // Stories are always public
                    PostType = "story"
                };

                if (storyType == "text")
                {
                    post.Content = form["storyText"];
                    post.MediaUrl = form["background"]; // This is the background gradient/color
                    post.MediaType = "StoryBackground"; 
                }
                else if (storyType == "image")
                {
                    HttpPostedFileBase mediaFile = Request.Files.Count > 0 ? Request.Files["imageInput"] : null;
                    if (mediaFile != null && mediaFile.ContentLength > 0)
                    {
                        var fileName = Path.GetFileName(mediaFile.FileName);
                        var fileExtension = Path.GetExtension(fileName).ToLower();
                        var uniqueFileName = Guid.NewGuid().ToString() + fileExtension;
                        var path = Path.Combine(Server.MapPath("~/Uploads/Stories/"), uniqueFileName);

                        Directory.CreateDirectory(Server.MapPath("~/Uploads/Stories/"));
                        mediaFile.SaveAs(path);

                        post.MediaUrl = "/Uploads/Stories/" + uniqueFileName;
                        post.MediaType = "Image";
                    }
                    else
                    {
                        return Json(new { success = false, message = "Vui lòng chọn một tệp ảnh." });
                    }
                }
                else
                {
                     return Json(new { success = false, message = "Loại tin không hợp lệ." });
                }
                
                db.Posts.Add(post);
                db.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // Log the exception (implementation needed)
                return Json(new { success = false, message = "Đã xảy ra lỗi không mong muốn khi tạo tin." });
            }
        }
    }
}