using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online_chat.Models
{
    public class PrivateMessage
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Sender")]
        public int SenderId { get; set; } // Phải là INT

        [ForeignKey("Receiver")]
        public int ReceiverId { get; set; } // Phải là INT

        [Required]
        public string Content { get; set; } // Nội dung JSON

        public string MessageType { get; set; } // "text", "image", "file", "call_log"

        public DateTime Timestamp { get; set; }

        public bool IsRead { get; set; }

        public virtual User Sender { get; set; }
        public virtual User Receiver { get; set; }
    }
}