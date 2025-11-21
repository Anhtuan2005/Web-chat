using System;

namespace Online_chat.Models
{
    public class UserActivityViewModel
    {
        public User User { get; set; }
        public int MessageCount { get; set; }
        public DateTime LastActivity { get; set; }
    }
}
