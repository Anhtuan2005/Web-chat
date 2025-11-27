using Online_chat.Models;
using System.Collections.Generic;
using System.Web.Mvc;
using System.Linq;
using System.Data.Entity;
using Microsoft.AspNet.Identity; 
using System.Web; 
using System.IO; 
using System;



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

            var storiesExist = db.Posts.Any(p => p.PostType == "story");
            if (!storiesExist)
            {
                var randomPost = db.Posts
                    .Include(p => p.User)
                    .Where(p => p.PostType == "post" && p.Privacy == "Public")
                    .OrderBy(p => Guid.NewGuid()) 
                    .FirstOrDefault();

                if (randomPost != null)
                {
                    ViewBag.FirstPostUrl = Url.Action("Details", "Feed", new { id = randomPost.Id });
                    ViewBag.FirstPostAvatar = randomPost.User.AvatarUrl;
                }
            }

            var posts = db.Posts
                .Include(p => p.User)
                .Include(p => p.Likes)
                .Include(p => p.Comments.Select(c => c.User))
                .Include(p => p.PostImages)
                .Where(p => p.PostType == "post") 
                .OrderByDescending(p => p.CreatedAt)
                .Take(10)
                .ToList();

            return View(posts);
        }


        // GET: Feed/Details/5
        public ActionResult Details(int id)
        {
            var username = User.Identity.Name;
            var user = db.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);
            ViewBag.CurrentUserId = user?.Id ?? 0;
            ViewBag.CurrentUserAvatar = user?.AvatarUrl ?? "/Content/default-avatar.png";

            var post = db.Posts
                .AsNoTracking() 
                .Include(p => p.User)
                .Include(p => p.Likes)
                .Include(p => p.Comments.Select(c => c.User))
                .Include(p => p.PostImages)
                .FirstOrDefault(p => p.Id == id);

            if (post == null)
            {
                return HttpNotFound();
            }

            if (post.Privacy == "Private" && post.UserId != user.Id)
            {
                return new HttpStatusCodeResult(System.Net.HttpStatusCode.Forbidden, "Bạn không có quyền xem bài viết này.");
            }

            var posts = new List<Post> { post };
            return View("Index", posts);
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
                .Include(p => p.PostImages)
                .Where(p => p.PostType == "post") 
                .OrderByDescending(p => p.CreatedAt)
                .Skip((page - 1) * pageSize)
                .Take(pageSize)
                .ToList();

            if (!posts.Any())
            {
                return Content("");
            }

            return PartialView("_PostListPartial", posts);
        }

        [HttpGet]
        public ActionResult GetStories()
        {
            try
            {
                var username = User.Identity.Name;
                var user = db.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);

                if (user == null)
                {
                    return Content("");
                }

                ViewBag.CurrentUserId = user.Id;
                ViewBag.CurrentUserAvatar = user.AvatarUrl ?? "/Content/default-avatar.png";
                var timeLimit = DateTime.Now.AddHours(-24);

                var stories = db.Posts
                    .Include(p => p.User)
                    .Include(p => p.PostImages)
                    .Where(p => p.PostType == "story" && p.CreatedAt >= timeLimit)
                    .OrderByDescending(p => p.CreatedAt) 
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"[GetStories] Tìm thấy {stories.Count} stories");

                var groupedStories = stories
                    .GroupBy(p => p.User)
                    .Select(g => new StoryViewModel
                    {
                        User = g.Key,
                        Stories = g.OrderBy(s => s.CreatedAt).ToList() 
                    })
                    .ToList();

                System.Diagnostics.Debug.WriteLine($"[GetStories] Grouped thành {groupedStories.Count} groups");

                return PartialView("_StoryListPartial", groupedStories);
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[GetStories] Lỗi: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[GetStories] Stack: {ex.StackTrace}");
                return Content("");
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
                    PostType = "post",
                    Content = Request.Form["content"],
                    PostBackground = Request.Form["postBackground"] ?? "default",
                    PostImages = new List<PostImage>()
                };

                if (string.IsNullOrWhiteSpace(post.Content) && Request.Files.Count == 0)
                {
                    return Json(new { success = false, message = "Bài viết phải có nội dung hoặc ảnh." });
                }

                if (Request.Files.Count > 0)
                {
                    string[] supportedImageTypes = { ".jpg", ".jpeg", ".png", ".gif" };
                    var uploadPath = Server.MapPath("~/Uploads/Posts/");
                    Directory.CreateDirectory(uploadPath);

                    for (int i = 0; i < Request.Files.Count; i++)
                    {
                        HttpPostedFileBase file = Request.Files[i]; 

                        if (file != null && file.ContentLength > 0)
                        {
                            var fileName = Path.GetFileName(file.FileName);
                            var fileExtension = Path.GetExtension(fileName).ToLower();

                            if (!supportedImageTypes.Contains(fileExtension))
                            {
                                continue;
                            }

                            var uniqueFileName = Guid.NewGuid().ToString() + fileExtension;
                            var path = Path.Combine(uploadPath, uniqueFileName);
                            file.SaveAs(path);

                            var postImage = new PostImage
                            {
                                ImageUrl = "/Uploads/Posts/" + uniqueFileName,
                            };

                            if (string.IsNullOrEmpty(post.MediaUrl))
                            {
                                post.MediaUrl = postImage.ImageUrl;
                                post.MediaType = "Image";
                            }

                            post.PostImages.Add(postImage);
                        }
                    }
                }

                db.Posts.Add(post);
                db.SaveChanges();

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                // System.Diagnostics.Debug.WriteLine(ex.ToString());
                return Json(new { success = false, message = "Đã xảy ra lỗi không mong muốn." });
            }
        }

        public ActionResult Events()
        {
            return View();
        }
        public ActionResult CreateStoryPage()
        {
            return View();
        }

        public ActionResult CreateStory(string type)
        {
            ViewBag.StoryType = type ?? "text";
            return View();
        }

        [HttpGet]
        public ActionResult GetSharedPost(int postId)
        {
            var post = db.Posts.Include(p => p.User).FirstOrDefault(p => p.Id == postId);
            if (post == null)
            {
                return HttpNotFound();
            }
            return PartialView("~/Views/Shared/_SharedPost.cshtml", post);
        }

        [HttpGet]
        public JsonResult GetFriendsToShare()
        {
            try
            {
                var username = User.Identity.Name;
                var currentUser = db.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);

                if (currentUser == null)
                {
                    return Json(new List<object>(), JsonRequestBehavior.AllowGet);
                }

                // Lấy danh sách ID của bạn bè (Status = Accepted)
                var friendIds = db.Friendships
                    .Where(f => f.Status == FriendshipStatus.Accepted &&
                                (f.SenderId == currentUser.Id || f.ReceiverId == currentUser.Id))
                    .Select(f => f.SenderId == currentUser.Id ? f.ReceiverId : f.SenderId)
                    .ToList();

                // Lấy thông tin user từ danh sách ID
                var friends = db.Users
                    .Where(u => friendIds.Contains(u.Id) && !u.IsDeleted)
                    .Select(u => new
                    {
                        Username = u.Username,
                        DisplayName = u.DisplayName ?? u.Username, // Ưu tiên hiển thị tên hiển thị
                        AvatarUrl = u.AvatarUrl
                    })
                    .ToList();

                return Json(friends, JsonRequestBehavior.AllowGet);
            }
            catch (Exception ex)
            {
                return Json(new List<object>(), JsonRequestBehavior.AllowGet);
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
                db.Likes.Remove(existingLike);
            }
            else
            {
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
                System.Diagnostics.Debug.WriteLine("[CreateStory] Bắt đầu tạo story");

                string username = User.Identity.Name;
                var currentUser = db.Users.FirstOrDefault(u => u.Username == username && !u.IsDeleted);

                if (currentUser == null)
                {
                    System.Diagnostics.Debug.WriteLine("[CreateStory] Không tìm thấy user");
                    return Json(new { success = false, message = "Vui lòng đăng nhập lại." });
                }

                System.Diagnostics.Debug.WriteLine($"[CreateStory] User: {currentUser.Username}");

                string storyType = form["storyType"];
                System.Diagnostics.Debug.WriteLine($"[CreateStory] Story Type: {storyType}");

                var post = new Post
                {
                    UserId = currentUser.Id,
                    CreatedAt = DateTime.Now,
                    Privacy = "Public",
                    PostType = "story", // QUAN TRỌNG: Đảm bảo PostType = "story"
                    PostImages = new List<PostImage>()
                };

                if (storyType == "text")
                {
                    post.Content = form["storyText"];
                    post.PostBackground = form["background"] ?? "linear-gradient(135deg, #667eea, #764ba2)";

                    System.Diagnostics.Debug.WriteLine($"[CreateStory] Text story - Content length: {post.Content?.Length ?? 0}");
                    System.Diagnostics.Debug.WriteLine($"[CreateStory] Background: {post.PostBackground}");

                    if (string.IsNullOrWhiteSpace(post.Content))
                    {
                        return Json(new { success = false, message = "Vui lòng nhập nội dung cho story." });
                    }
                }
                else if (storyType == "image")
                {
                    System.Diagnostics.Debug.WriteLine($"[CreateStory] Image story - Files count: {Request.Files.Count}");

                    if (Request.Files.Count > 0 && Request.Files["imageInput"] != null)
                    {
                        var mediaFile = Request.Files["imageInput"];
                        System.Diagnostics.Debug.WriteLine($"[CreateStory] File size: {mediaFile.ContentLength}");

                        if (mediaFile.ContentLength > 0)
                        {
                            var fileName = Path.GetFileName(mediaFile.FileName);
                            var fileExtension = Path.GetExtension(fileName).ToLower();
                            var uniqueFileName = Guid.NewGuid().ToString() + fileExtension;

                            var pathDir = Server.MapPath("~/Uploads/Stories/");
                            if (!Directory.Exists(pathDir))
                            {
                                Directory.CreateDirectory(pathDir);
                                System.Diagnostics.Debug.WriteLine($"[CreateStory] Created directory: {pathDir}");
                            }

                            var path = Path.Combine(pathDir, uniqueFileName);
                            mediaFile.SaveAs(path);

                            var imageUrl = "/Uploads/Stories/" + uniqueFileName;
                            post.MediaUrl = imageUrl;
                            post.MediaType = "Image";
                            post.PostImages.Add(new PostImage { ImageUrl = imageUrl });

                            System.Diagnostics.Debug.WriteLine($"[CreateStory] Image saved: {imageUrl}");
                        }
                    }
                    else
                    {
                        System.Diagnostics.Debug.WriteLine("[CreateStory] No image file found");
                        return Json(new { success = false, message = "Vui lòng chọn ảnh." });
                    }
                }

                db.Posts.Add(post);
                db.SaveChanges();

                System.Diagnostics.Debug.WriteLine($"[CreateStory] Story created successfully with ID: {post.Id}");
                System.Diagnostics.Debug.WriteLine($"[CreateStory] PostType: {post.PostType}");

                return Json(new { success = true });
            }
            catch (Exception ex)
            {
                System.Diagnostics.Debug.WriteLine($"[CreateStory] ERROR: {ex.Message}");
                System.Diagnostics.Debug.WriteLine($"[CreateStory] Stack: {ex.StackTrace}");
                return Json(new { success = false, message = "Lỗi server: " + ex.Message });
            }
        }
    }
}