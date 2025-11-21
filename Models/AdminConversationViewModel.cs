using System;

namespace Online_chat.Models
{
    public class AdminConversationViewModel
    {
        public User User1 { get; set; }
        public User User2 { get; set; }
        public int MessageCount { get; set; }
        public DateTime LastMessageTimestamp { get; set; }
        public string LastMessageContent { get; set; }
    }
}
