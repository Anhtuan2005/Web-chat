using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online_chat.Models
{
    public class Group
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [StringLength(100)]
        public string Name { get; set; }

        public int CreatedById { get; set; }

        public string AvatarUrl { get; set; }

        [Required]
        public string CreatedBy { get; set; }

        public DateTime CreatedAt { get; set; }

        public int? OwnerId { get; set; }

        // Navigation properties
        public virtual ICollection<GroupMember> Members { get; set; }
        public virtual ICollection<GroupMessage> Messages { get; set; }

        [ForeignKey("OwnerId")]
        public virtual User Owner { get; set; }
    }

    // ⭐ SỬA GroupMember - DÙNG UserId
    public class GroupMember
    {
        [Key]
        public int Id { get; set; }

        public int GroupId { get; set; }

        public int UserId { get; set; }  

        [StringLength(20)]
        public string Role { get; set; }

        public DateTime JoinedAt { get; set; }

        [ForeignKey("GroupId")]
        public virtual Group Group { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }
    public class GroupMessage
    {
        [Key]
        public int Id { get; set; }

        public int GroupId { get; set; }

        public int SenderId { get; set; } 

        [Required]
        public string Content { get; set; }

        public DateTime Timestamp { get; set; }

        [ForeignKey("GroupId")]
        public virtual Group Group { get; set; }

        [ForeignKey("SenderId")]
        public virtual User Sender { get; set; }
        public bool IsDeleted { get; set; }      
        public DateTime? EditedAt { get; set; }
    }
}