using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;

namespace Online_chat.Controllers
{
    [Authorize] // Chỉ người dùng đã đăng nhập mới được upload
    public class UploadController : Controller
    {
        [HttpPost]
        public JsonResult Image(HttpPostedFileBase file)
        {
            try
            {
                if (file == null || file.ContentLength == 0)
                {
                    return Json(new { success = false, message = "Không có file nào được chọn." });
                }

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif" };
                var extension = Path.GetExtension(file.FileName).ToLower();
                if (!allowedExtensions.Contains(extension))
                {
                    return Json(new { success = false, message = "Định dạng file không được hỗ trợ." });
                }

                var fileName = Guid.NewGuid().ToString() + extension;

                var serverPath = Path.Combine(Server.MapPath("~/Uploads/Images"), fileName);

                file.SaveAs(serverPath);

                var relativePath = "/Uploads/Images/" + fileName;

                return Json(new { success = true, filePath = relativePath });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Đã xảy ra lỗi trong quá trình upload." });
            }
        }
    }
}