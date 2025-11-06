using Online_chat.Models;
using System;
using System.Linq;
using System.Threading.Tasks;
using System.Web.Mvc;

namespace Online_chat.Controllers
{
    [Authorize] 
    public class HomeController : Controller
    {
        public ActionResult Index()
        {
            return RedirectToAction("Index", "Chat");
        }

        public ActionResult TestSqlConnection()
        {
            try
            {
                using (var context = new ApplicationDbContext())
                {
                    var messageCount = context.Messages.Count();
                    ViewBag.Status = "Thành công!";
                    ViewBag.Message = $"Kết nối tới SQL Server thành công! Hiện có {messageCount} tin nhắn trong bảng ChatMessages.";
                }
            }
            catch (Exception ex)
            {
                ViewBag.Status = "Thất bại!";
                var baseException = ex.GetBaseException();
                ViewBag.Message = "Không thể kết nối tới SQL Server. Lỗi: " + baseException.Message;
            }
            return View("TestMongoConnection");
        }

        public ActionResult About()
        {
            ViewBag.Message = "Your application description page.";
            return View();
        }

        public ActionResult Contact()
        {
            ViewBag.Message = "Your contact page.";
            return View();
        }
    }
}