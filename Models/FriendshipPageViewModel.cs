using System.Collections.Generic;

namespace Online_chat.Models
{
    public class FriendshipPageViewModel
    {
        public List<FriendItemViewModel> Friends { get; set; }
        public List<FriendRequestViewModel> PendingRequests { get; set; }
    }
    public class FriendItemViewModel
    {
        public int FriendshipId { get; set; }
        public string FriendDisplayName { get; set; }
        public string FriendUsername { get; set; }
        public string FriendAvatarUrl { get; set; } 
    }
}