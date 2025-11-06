using System.Collections.Generic;

namespace Online_chat.Models
{
    public class FriendViewModel
    {
        public List<Friendship> Friends { get; set; }
        public List<Friendship> PendingRequests { get; set; }
    }
}