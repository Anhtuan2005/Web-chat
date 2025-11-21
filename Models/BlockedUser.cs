using Online_chat.Models; 

namespace Online_chat.Models
{
    public class BlockedUser
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public int BlockedUserId { get; set; }

        public virtual User User { get; set; }
        public virtual User BlockedUserEntity { get; set; }
    }
}