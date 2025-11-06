using Online_chat.Models;
using System.Linq;
using System.Web.Mvc;

namespace Online_chat.Controllers
{
    [Authorize]
    public class BaseController : Controller
    {
        protected readonly ApplicationDbContext _context = new ApplicationDbContext();

        protected override void OnActionExecuting(ActionExecutingContext filterContext)
        {
            if (User.Identity.IsAuthenticated)
            {
                var currentUsername = User.Identity.Name;
                var currentUser = _context.Users.FirstOrDefault(u => u.Username == currentUsername);

                if (currentUser != null)
                {
                    ViewBag.CurrentUserAvatarUrl = currentUser.AvatarUrl;
                    ViewBag.CurrentUserAvatarVersion = currentUser.AvatarVersion;
                }
            }
            base.OnActionExecuting(filterContext);
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