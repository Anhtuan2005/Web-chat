using System;
using System.Collections.Generic;
using System.ComponentModel;

namespace Online_chat.Models
{
    public class User
    {
        public int Id { get; set; }
        public string UserCode { get; set; }
        public string Username { get; set; }
        public string PasswordHash { get; set; }
        public string DisplayName { get; set; }
        public string Email { get; set; }
        public string PhoneNumber { get; set; }
        public string AvatarUrl { get; set; }
        public long AvatarVersion { get; set; }
        public string CoverPhotoUrl { get; set; }
        public bool IsAdmin { get; set; }

        [DefaultValue(false)]
        public bool IsDeleted { get; set; }
        public DateTime CreatedAt { get; set; }

        public string Gender { get; set; } 

        public DateTime? DateOfBirth { get; set; }

        public string Bio { get; set; }
        public virtual ICollection<Message> SentMessages { get; set; }
        public virtual ICollection<Friendship> InitiatedFriendships { get; set; }
        public virtual ICollection<Friendship> ReceivedFriendships { get; set; }
    }
}