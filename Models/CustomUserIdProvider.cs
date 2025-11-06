using Microsoft.AspNet.SignalR;

namespace Online_chat.Infrastructure
{
    public class CustomUserIdProvider : IUserIdProvider
    {
        public string GetUserId(IRequest request)
        {
            if (request.User != null && request.User.Identity.IsAuthenticated)
            {
                return request.User.Identity.Name;
            }
            return null;
        }
    }
}