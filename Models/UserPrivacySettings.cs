using Online_chat.Models; 

namespace Online_chat.Models
{
    public class UserPrivacySettings
    {
        public int Id { get; set; }
        public int UserId { get; set; }
        public bool IsProfilePublic { get; set; }
        public bool CanReceiveMessages { get; set; }

        public virtual User User { get; set; }
    }
}