using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;

namespace Online_chat.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(50)]
        public string Username { get; set; }

        [Required]
        [StringLength(100)]
        public string DisplayName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(100)]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public string PhoneNumber { get; set; }

        public string AvatarUrl { get; set; }

        public long AvatarVersion { get; set; }

        public string CoverPhotoUrl { get; set; }

        public string Gender { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string Bio { get; set; }

        public bool IsAdmin { get; set; }

        public bool IsDeleted { get; set; }

        public DateTime CreatedAt { get; set; }

        [StringLength(50)]
        public string UserCode { get; set; }
        public string FullName { get; set; }


        // Navigation properties
        public virtual UserPrivacySettings PrivacySettings { get; set; }
        public virtual ICollection<GroupMember> GroupMembers { get; set; }
        public virtual ICollection<GroupMessage> SentGroupMessages { get; set; }
        public virtual ICollection<PrivateMessage> SentPrivateMessages { get; set; }
        public virtual ICollection<PrivateMessage> ReceivedPrivateMessages { get; set; }
        public virtual ICollection<Friendship> SentFriendRequests { get; set; }
        public virtual ICollection<Friendship> ReceivedFriendRequests { get; set; }
        public virtual ICollection<Post> Posts { get; set; }
        public virtual ICollection<Like> Likes { get; set; }
        public virtual ICollection<Comment> Comments { get; set; }
    }
}