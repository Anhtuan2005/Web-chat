using System;

namespace Online_chat.Models
{
    public class ConversationViewModel
    {
        public string Type { get; set; }
        public string Id { get; set; } 
        public string Username { get; set; }
        public string DisplayName { get; set; }
        public string Name { get; set; }
        public string AvatarUrl { get; set; }
        public System.Collections.Generic.List<string> MemberAvatarUrls { get; set; }
        public string LastMessage { get; set; }
        public DateTime LastMessageTimestamp { get; set; }
        public int UnreadCount { get; set; }
        public bool IsPinned { get; set; } 
        public DateTime PinnedAt { get; set; }
    }
}