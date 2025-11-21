using System.Collections.Generic;

namespace Online_chat.Models
{
    public class NotificationViewModel
    {
        public List<PrivateMessage> RecentUnreadMessages { get; set; }
        public List<Report> RecentUnresolvedReports { get; set; }
        public int UnreadMessagesCount { get; set; }
        public int UnresolvedReportsCount { get; set; }
    }
}
