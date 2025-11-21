using System;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online_chat.Models
{
    public class PinnedConversation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Index("IX_UserAndConversation", 1, IsUnique = true)]
        public int UserId { get; set; }  

        [Required]
        [Index("IX_UserAndConversation", 2, IsUnique = true)]
        [MaxLength(128)]
        public string ConversationId { get; set; }

        [Required]
        [Index("IX_UserAndConversation", 3, IsUnique = true)]
        [MaxLength(50)]
        public string ConversationType { get; set; }

        public DateTime PinnedAt { get; set; }

        [ForeignKey("UserId")]
        public virtual User User { get; set; }
    }
}