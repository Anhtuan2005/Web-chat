using System;
using System.IO;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Collections.Generic;

namespace Online_chat.Controllers
{
    [Authorize]
    public class UploadController : Controller
    {
        private const int MAX_FILE_SIZE = 50 * 1024 * 1024; // 50MB
        private const int MAX_IMAGE_SIZE = 10 * 1024 * 1024; // 10MB
        private const int MAX_VIDEO_SIZE = 100 * 1024 * 1024; // 100MB

        [HttpPost]
        public JsonResult Image(HttpPostedFileBase file)
        {
            try
            {
                if (file == null || file.ContentLength == 0)
                    return Json(new { success = false, message = "Không có file nào được chọn." });

                if (file.ContentLength > MAX_IMAGE_SIZE)
                    return Json(new { success = false, message = $"Kích thước ảnh vượt quá {MAX_IMAGE_SIZE / (1024 * 1024)}MB." });

                var allowedExtensions = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
                var extension = Path.GetExtension(file.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                    return Json(new { success = false, message = "Chỉ hỗ trợ file ảnh (jpg, png, gif, bmp, webp)." });

                var fileName = Guid.NewGuid().ToString() + extension;
                var uploadDir = Server.MapPath("~/Uploads/Images");

                if (!Directory.Exists(uploadDir))
                    Directory.CreateDirectory(uploadDir);

                var serverPath = Path.Combine(uploadDir, fileName);
                file.SaveAs(serverPath);

                var timestamp = DateTime.Now.Ticks;
                var relativePath = $"/Uploads/Images/{fileName}?v={timestamp}";

                return Json(new { success = true, filePath = relativePath, type = "image" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi upload ảnh: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Video(HttpPostedFileBase file)
        {
            try
            {
                if (file == null || file.ContentLength == 0)
                    return Json(new { success = false, message = "Không có file nào được chọn." });

                if (file.ContentLength > MAX_VIDEO_SIZE)
                    return Json(new { success = false, message = $"Kích thước video vượt quá {MAX_VIDEO_SIZE / (1024 * 1024)}MB." });

                var allowedExtensions = new[] { ".mp4", ".mov", ".avi", ".webm", ".mkv", ".flv" };
                var extension = Path.GetExtension(file.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                    return Json(new { success = false, message = "Chỉ hỗ trợ file video (mp4, mov, avi, webm, mkv)." });

                var fileName = Guid.NewGuid().ToString() + extension;
                var uploadDir = Server.MapPath("~/Uploads/Videos");

                if (!Directory.Exists(uploadDir))
                    Directory.CreateDirectory(uploadDir);

                var serverPath = Path.Combine(uploadDir, fileName);
                file.SaveAs(serverPath);

                var timestamp = DateTime.Now.Ticks;
                var relativePath = $"/Uploads/Videos/{fileName}?v={timestamp}";

                return Json(new { success = true, filePath = relativePath, type = "video" });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi upload video: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult File(HttpPostedFileBase file)
        {
            try
            {
                if (file == null || file.ContentLength == 0)
                    return Json(new { success = false, message = "Không có file nào được chọn." });

                if (file.ContentLength > MAX_FILE_SIZE)
                    return Json(new { success = false, message = $"Kích thước file vượt quá {MAX_FILE_SIZE / (1024 * 1024)}MB." });

                var allowedExtensions = new[] {
                    ".pdf", ".doc", ".docx", ".xls", ".xlsx", ".ppt", ".pptx",
                    ".txt", ".zip", ".rar", ".7z", ".csv"
                };
                var extension = Path.GetExtension(file.FileName).ToLower();

                if (!allowedExtensions.Contains(extension))
                    return Json(new { success = false, message = "Định dạng file không được hỗ trợ." });

                var fileName = Guid.NewGuid().ToString() + extension;
                var uploadDir = Server.MapPath("~/Uploads/Files");

                if (!Directory.Exists(uploadDir))
                    Directory.CreateDirectory(uploadDir);

                var serverPath = Path.Combine(uploadDir, fileName);
                file.SaveAs(serverPath);

                var timestamp = DateTime.Now.Ticks;
                var relativePath = $"/Uploads/Files/{fileName}?v={timestamp}";

                return Json(new
                {
                    success = true,
                    filePath = relativePath,
                    type = "file",
                    fileName = file.FileName, // Tên file gốc
                    fileSize = FormatFileSize(file.ContentLength)
                });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi upload file: " + ex.Message });
            }
        }

        [HttpPost]
        public JsonResult Multiple(IEnumerable<HttpPostedFileBase> files)
        {
            try
            {
                var uploadedFiles = new List<object>();

                if (files == null || !files.Any())
                {
                    return Json(new { success = false, message = "Không có file nào được chọn." });
                }

                foreach (var file in files)
                {
                    if (file == null || file.ContentLength == 0) continue;

                    if (file.ContentLength > 50 * 1024 * 1024)
                    {
                        // Trả về lỗi ngay lập tức nếu có file quá lớn
                        return Json(new { success = false, message = $"File '{file.FileName}' vượt quá 50MB" });
                    }

                    var extension = Path.GetExtension(file.FileName).ToLower();
                    var fileName = Guid.NewGuid().ToString() + extension;
                    string uploadDir;
                    string type;

                    var imageExts = new[] { ".jpg", ".jpeg", ".png", ".gif", ".bmp", ".webp" };
                    var videoExts = new[] { ".mp4", ".mov", ".avi", ".webm", ".mkv" };

                    if (imageExts.Contains(extension))
                    {
                        uploadDir = Server.MapPath("~/Uploads/Images");
                        type = "image";
                    }
                    else if (videoExts.Contains(extension))
                    {
                        uploadDir = Server.MapPath("~/Uploads/Videos");
                        type = "video";
                    }
                    else
                    {
                        uploadDir = Server.MapPath("~/Uploads/Files");
                        type = "file";
                    }

                    if (!Directory.Exists(uploadDir))
                        Directory.CreateDirectory(uploadDir);

                    var serverPath = Path.Combine(uploadDir, fileName);
                    file.SaveAs(serverPath);

                    var timestamp = DateTime.Now.Ticks;
                    var folderName = type == "image" ? "Images" : (type == "video" ? "Videos" : "Files");
                    var relativePath = $"/Uploads/{folderName}/{fileName}?v={timestamp}";

                    uploadedFiles.Add(new
                    {
                        filePath = relativePath,
                        type = type,
                        fileName = file.FileName,
                        fileSize = FormatFileSize(file.ContentLength)
                    });
                }

                if (uploadedFiles.Count == 0)
                    return Json(new { success = false, message = "Không có file hợp lệ nào được upload." });

                return Json(new { success = true, files = uploadedFiles });
            }
            catch (Exception ex)
            {
                return Json(new { success = false, message = "Lỗi upload: " + ex.Message });
            }
        }


        [HttpPost]
        public JsonResult Voice(HttpPostedFileBase voice)
        {
            try
            {
                if (voice == null || voice.ContentLength == 0)
                    return Json(new { success = false, message = "Không có file ghi âm nào được gửi." });

                // Giới hạn kích thước file ghi âm, ví dụ 5MB
                if (voice.ContentLength > 5 * 1024 * 1024)
                    return Json(new { success = false, message = "File ghi âm vượt quá 5MB." });

                var fileName = Guid.NewGuid().ToString() + ".wav";
                var uploadDir = Server.MapPath("~/Uploads/Voice");

                if (!Directory.Exists(uploadDir))
                    Directory.CreateDirectory(uploadDir);

                var serverPath = Path.Combine(uploadDir, fileName);
                voice.SaveAs(serverPath);

                var timestamp = DateTime.Now.Ticks;
                var relativePath = $"/Uploads/Voice/{fileName}?v={timestamp}";

                return Json(new
                {
                    success = true,
                    filePath = relativePath,
                    fileSize = FormatFileSize(voice.ContentLength)
                });
            }
            catch (Exception ex)
            {
                // Log the exception
                System.Diagnostics.Debug.WriteLine("Voice upload error: " + ex.Message);
                return Json(new { success = false, message = "Lỗi khi xử lý file ghi âm." });
            }
        }

        private string FormatFileSize(long bytes)
        {
            string[] sizes = { "B", "KB", "MB", "GB" };
            double len = bytes;
            int order = 0;
            while (len >= 1024 && order < sizes.Length - 1)
            {
                order++;
                len = len / 1024;
            }
            return $"{len:0.##} {sizes[order]}";
        }
    }
}