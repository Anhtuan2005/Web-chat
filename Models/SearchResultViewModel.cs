namespace Online_chat.Models
{
    public class SearchResultViewModel
    {
        public int UserId { get; set; }
        public string DisplayName { get; set; }
        public string Username { get; set; }
        public FriendshipStatus? FriendshipStatus { get; set; } 
        public string AvatarUrl { get; set; } 
        public string CoverPhotoUrl { get; set; }
        public string UserCode { get; set; } 
    }
}