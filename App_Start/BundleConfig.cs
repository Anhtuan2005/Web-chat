using System.Web.Optimization;

namespace Online_chat
{
    public class BundleConfig
    {
        public static void RegisterBundles(BundleCollection bundles)
        {
            bundles.Add(new ScriptBundle("~/bundles/jquery").Include(
                "~/Scripts/jquery-{version}.js"));

            bundles.Add(new ScriptBundle("~/bundles/jqueryval").Include(
                "~/Scripts/jquery.validate*"));

            bundles.Add(new ScriptBundle("~/bundles/modernizr").Include(
                "~/Scripts/modernizr-*"));

            bundles.Add(new Bundle("~/bundles/popper").Include(
                "~/Scripts/umd/popper.min.js"));

            bundles.Add(new Bundle("~/bundles/bootstrap").Include(
                "~/Scripts/bootstrap.bundle.min.js"));

            bundles.Add(new Bundle("~/bundles/picmo").Include(
                         "~/Scripts/picmo.min.js",
                         "~/Scripts/picmo-popup.min.js"));

            bundles.Add(new StyleBundle("~/Content/chat-styles").Include(
                "~/Content/chat-styles.css"));

            bundles.Add(new StyleBundle("~/Content/css").Include(
                "~/Content/bootstrap.min.css",
                "~/Content/site.css"));
        }
    }
}