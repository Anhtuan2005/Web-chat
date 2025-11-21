using System;
using System.Collections.Generic;
using System.Data.Entity.Migrations;
using System.Linq;
using System.Web;
using System.Web.Mvc;
using System.Web.Optimization;
using System.Web.Routing;
using WebChat_Online_MVC.Migrations;

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

            // Tự động chạy migration
            var configuration = new Configuration();
            var migrator = new DbMigrator(configuration);
            migrator.Update();
        }
    }
}
