using System;
using System.Collections.Generic;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;

namespace Online_chat
{
    public class MvcApplication : System.Web.HttpApplication
    {
        protected void Application_Start()
        {
            // Đăng ký tất cả các Area
            AreaRegistration.RegisterAllAreas();

            // Đăng ký bộ lọc toàn cục
            FilterConfig.RegisterGlobalFilters(GlobalFilters.Filters);

            // Đăng ký Route
            RouteConfig.RegisterRoutes(RouteTable.Routes);

            // Đăng ký Bundle (CSS, JS, v.v.)
            BundleConfig.RegisterBundles(BundleTable.Bundles);
        }
    }
}
