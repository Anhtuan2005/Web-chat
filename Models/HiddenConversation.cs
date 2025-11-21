using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online_chat.Models
{
    public class HiddenConversation
    {
        [Key]
        public int Id { get; set; }

        [Required]
        [Index("IX_UserAndPartner", 1, IsUnique = true)]
        [MaxLength(128)]
        public string UserId { get; set; }

        [Required]
        [Index("IX_UserAndPartner", 2, IsUnique = true)]
        [MaxLength(256)]
        public string PartnerUsername { get; set; }
    }
}
