using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online_chat.Models
{
    public class Group
    {
        public int Id { get; set; }
        public string GroupName { get; set; }

        public DateTime CreatedAt { get; set; }

        public int OwnerId { get; set; }
        [ForeignKey("OwnerId")]
        public virtual User Owner { get; set; }

        public virtual ICollection<Message> Messages { get; set; }
    }
}