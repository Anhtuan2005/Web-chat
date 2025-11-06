using System;

namespace Online_chat.Models
{
    public class Message
    {
        public int Id { get; set; }
        public string Content { get; set; }
        public DateTime Timestamp { get; set; }

        public int SenderId { get; set; }
        public virtual User Sender { get; set; }

        public int GroupId { get; set; }
        public virtual Group Group { get; set; }

    }
}