using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online_chat.Models
{

    public class Friendship
    {
        [Key]
        public int Id { get; set; }

        [ForeignKey("Sender")]
        public int SenderId { get; set; } // Phải là INT

        [ForeignKey("Receiver")]
        public int ReceiverId { get; set; } // Phải là INT

        public FriendshipStatus Status { get; set; }

        public DateTime RequestedAt { get; set; }
        public DateTime? RespondedAt { get; set; }

        // Navigation properties
        public virtual User Sender { get; set; }
        public virtual User Receiver { get; set; }
    }
}