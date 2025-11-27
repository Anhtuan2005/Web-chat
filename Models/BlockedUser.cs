using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online_chat.Models
{
    public class BlockedUser
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Index("IX_Blocker_Blocked", 1, IsUnique = true)]
        public int BlockerId { get; set; }

        [Required]
        [Index("IX_Blocker_Blocked", 2, IsUnique = true)]
        public int BlockedId { get; set; }

        [ForeignKey("BlockerId")]
        public virtual User Blocker { get; set; }

        [ForeignKey("BlockedId")]
        public virtual User Blocked { get; set; }
    }
}