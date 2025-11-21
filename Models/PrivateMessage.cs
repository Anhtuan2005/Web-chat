using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;
using System.Collections.Generic;
namespace Online_chat.Models
{
    public class PrivateMessage
    {
        [Key]
        public int Id { get; set; }

        public int SenderId { get; set; }   
        public int ReceiverId { get; set; }

        [Required]
        public string Content { get; set; }

        public DateTime Timestamp { get; set; }

        public bool IsRead { get; set; } = false; 

        public DateTime? ReadAt { get; set; }
        public DateTime? DeliveredAt { get; set; }

        [StringLength(20)]
        public string MessageType { get; set; } = "text";

        public MessageStatus Status { get; set; } = MessageStatus.Sent; 

        public DateTime? EditedAt { get; set; }
        public bool IsDeleted { get; set; } = false;

        public DateTime? DeletedAt { get; set; }

        public int? ParentMessageId { get; set; }
        public virtual PrivateMessage ParentMessage { get; set; }
        public virtual ICollection<PrivateMessage> Replies { get; set; }
        public virtual ICollection<MessageReaction> Reactions { get; set; }

        public int? ForwardedFromId { get; set; }
        public virtual User ForwardedFrom { get; set; }

        // Navigation properties
        [ForeignKey("SenderId")]
        public virtual User Sender { get; set; }

        [ForeignKey("ReceiverId")]
        public virtual User Receiver { get; set; }
    }

    public enum MessageStatus
    {
        Sent = 0,       // Đã gửi ✓
        Delivered = 1,  // Đã nhận ✓✓
        Read = 2        // Đã xem ✓✓ (xanh)
    }
}