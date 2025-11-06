using Online_chat.Hubs;
using Online_chat.Models;
using Microsoft.AspNet.SignalR; 
using System;
using System.Data.Entity;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using QRCoder; 
using System.Drawing;
using System.Drawing.Imaging;

namespace Online_chat.Controllers
{
    public class ProfileController : BaseController
    {
        private readonly ApplicationDbContext _context = new ApplicationDbContext();

        public ActionResult Index()
        {
            var currentUsername = User.Identity.Name;
            var userProfile = _context.Users.FirstOrDefault(u => u.Username == currentUsername);
            if (userProfile == null)
            {
                return HttpNotFound();
            }

            ViewBag.CurrentUserAvatarUrl = userProfile?.AvatarUrl;
            ViewBag.CurrentUserAvatarVersion = userProfile?.AvatarVersion ?? 0;

            return View(userProfile); 
        }

        [HttpPost]
        [ValidateAntiForgeryToken] 
        public ActionResult Index(User updatedProfile, HttpPostedFileBase avatarFile, HttpPostedFileBase coverFile)
        {
            var currentUsername = User.Identity.Name;
            var userInDb = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (userInDb == null)
            {
                return HttpNotFound();
            }

            bool avatarChanged = false;

            if (avatarFile != null && avatarFile.ContentLength > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(avatarFile.FileName);
                var folderPath = Server.MapPath("~/Uploads/Avatars");
                Directory.CreateDirectory(folderPath);
                var path = Path.Combine(folderPath, fileName);
                avatarFile.SaveAs(path);
                userInDb.AvatarUrl = "/Uploads/Avatars/" + fileName;

                userInDb.AvatarVersion = DateTime.UtcNow.Ticks;
                avatarChanged = true;
            }

            if (coverFile != null && coverFile.ContentLength > 0)
            {
                var fileName = Guid.NewGuid().ToString() + Path.GetExtension(coverFile.FileName);
                var folderPath = Server.MapPath("~/Uploads/CoverPhotos");
                Directory.CreateDirectory(folderPath); 
                var path = Path.Combine(folderPath, fileName);
                coverFile.SaveAs(path); 
                userInDb.CoverPhotoUrl = "/Uploads/CoverPhotos/" + fileName;
            }

            userInDb.DisplayName = updatedProfile.DisplayName;
            userInDb.PhoneNumber = updatedProfile.PhoneNumber;

            _context.Entry(userInDb).State = EntityState.Modified;
            _context.SaveChanges();

            if (avatarChanged)
            {
                var newAvatarUrlWithVersion = $"{userInDb.AvatarUrl}?v={userInDb.AvatarVersion}";

                var hubContext = GlobalHost.ConnectionManager.GetHubContext<ChatHub>();
                hubContext.Clients.User(userInDb.Username).updateAvatar(newAvatarUrlWithVersion);
            }

            TempData["SuccessMessage"] = "Cập nhật thông tin thành công!";
            return RedirectToAction("Index");
        }
        [HttpGet]
        public JsonResult GetMyProfileSummary()
        {
            var currentUsername = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (user == null)
            {
                return Json(new { success = false }, JsonRequestBehavior.AllowGet);
            }
            var qrCodeUrl = Url.Action("GetMyQrCode", "Profile");

            return Json(new
            {
                success = true,
                displayName = user.DisplayName,
                phoneNumber = user.PhoneNumber,
                email = user.Email,
                avatarUrl = $"{user.AvatarUrl}?v={user.AvatarVersion}",
                coverUrl = user.CoverPhotoUrl,
                qrCodeUrl = qrCodeUrl
            }, JsonRequestBehavior.AllowGet);
        }

        [HttpGet]
        public ActionResult GetMyQrCode()
        {
            var currentUsername = User.Identity.Name;
            var user = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

            if (user == null || string.IsNullOrEmpty(user.UserCode))
            {
                return HttpNotFound();
            }

            QRCodeGenerator qrGenerator = new QRCodeGenerator();
            QRCodeData qrCodeData = qrGenerator.CreateQrCode(user.UserCode, QRCodeGenerator.ECCLevel.Q);
            PngByteQRCode qrCode = new PngByteQRCode(qrCodeData);
            byte[] qrCodeAsPngByteArr = qrCode.GetGraphic(10); 

            return File(qrCodeAsPngByteArr, "image/png");
        }
    }
}