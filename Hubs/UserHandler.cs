using System.Collections.Concurrent;

namespace Online_chat.Hubs
{
    public static class UserHandler
    {
        public static ConcurrentDictionary<string, string> ConnectedIds = new ConcurrentDictionary<string, string>();

        public static ConcurrentDictionary<string, string> UsernameToConnectionId = new ConcurrentDictionary<string, string>();
    }
}