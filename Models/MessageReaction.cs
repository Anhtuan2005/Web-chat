using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online_chat.Models
{
    public class MessageReaction
    {
        [Key]
        public int Id { get; set; }

        public int MessageId { get; set; }

        public int UserId { get; set; }

        [Required]
        [StringLength(50)]
        public string Emoji { get; set; } 

        public DateTime CreatedAt { get; set; } = DateTime.Now;

        // Navigation properties
        [ForeignKey("MessageId")]
        public virtual PrivateMessage Message { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }
}