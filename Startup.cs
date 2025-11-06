using Microsoft.AspNet.SignalR;
using Microsoft.Owin;
using Owin;
using Online_chat.Infrastructure;
using Newtonsoft.Json;  
using System.Diagnostics;

[assembly: OwinStartup(typeof(Online_chat.Startup))]
namespace Online_chat
{
    public class Startup
    {
        static Startup()
        {
            var version = typeof(JsonConvert).Assembly.GetName().Version;
            Debug.WriteLine($"[DEBUG] Đã ép tải Newtonsoft.Json phiên bản: {version}");
        }

        public void Configuration(IAppBuilder app)
        {
            GlobalHost.DependencyResolver.Register(typeof(IUserIdProvider), () => new CustomUserIdProvider());

            app.MapSignalR();
        }
    }
}