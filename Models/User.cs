using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online_chat.Models
{
    public class User
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(256)]
        [Index(IsUnique = true)]
        public string Username { get; set; }

        [Required]
        [StringLength(100)]
        public string DisplayName { get; set; }

        [Required]
        [EmailAddress]
        [StringLength(256)]
        [Index(IsUnique = true)]
        public string Email { get; set; }

        [Required]
        public string PasswordHash { get; set; }

        public string AvatarUrl { get; set; }

        public string CoverPhotoUrl { get; set; }

        public string Bio { get; set; }

        public string Gender { get; set; }

        public DateTime? DateOfBirth { get; set; }

        public string PhoneNumber { get; set; }

        [StringLength(8)]
        [Index(IsUnique = true)]
        public string UserCode { get; set; }

        public long AvatarVersion { get; set; } = 0; 

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        public bool IsAdmin { get; set; } = false;

        public bool IsDeleted { get; set; } = false;

        // Navigation properties
        public virtual ICollection<Message> SentMessages { get; set; }
        public virtual ICollection<Friendship> InitiatedFriendships { get; set; }
        public virtual ICollection<Friendship> ReceivedFriendships { get; set; }
    }
}