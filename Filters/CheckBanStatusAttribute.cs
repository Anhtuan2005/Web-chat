using Online_chat.Models;
using System;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Security;

namespace Online_chat.Filters
{
    public class CheckBanStatusAttribute : ActionFilterAttribute
    {
        public override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            var user = filterContext.HttpContext.User;

            // Chỉ kiểm tra nếu user đã đăng nhập
            if (user != null && user.Identity.IsAuthenticated)
            {
                using (var db = new ApplicationDbContext())
                {
                    var username = user.Identity.Name;
                    var dbUser = db.Users.FirstOrDefault(u => u.Username == username);

                    // Nếu user bị xóa hoặc đang bị khóa
                    if (dbUser != null && (dbUser.IsDeleted || (dbUser.BanExpiresAt != null && dbUser.BanExpiresAt > DateTime.Now)))
                    {
                        // 1. Đăng xuất
                        FormsAuthentication.SignOut();

                        // 2. Chuyển hướng về trang Login
                        filterContext.Result = new RedirectToRouteResult(
                            new System.Web.Routing.RouteValueDictionary(
                                new { controller = "Account", action = "Login" }
                            )
                        );
                    }
                }
            }
            base.OnActionExecuting(filterContext);
        }
    }
}