using System;

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

        public string Gender { get; set; }
        public DateTime? DateOfBirth { get; set; } 
        public string Bio { get; set; }
        public string PhoneNumber { get; set; }
        public string Email { get; set; }

    }
}