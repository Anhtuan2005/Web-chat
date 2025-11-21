using System;
using System.Collections.Generic;
using System.ComponentModel.DataAnnotations;
using System.ComponentModel.DataAnnotations.Schema;

namespace Online_chat.Models
{
    public class Post
    {
        [Key]
        public int Id { get; set; }

        public string Content { get; set; }

        public string MediaUrl { get; set; }

        // "Image", "Video", or null
        public string MediaType { get; set; }

        public string PostType { get; set; }

        public DateTime CreatedAt { get; set; }

        [ForeignKey("User")]
        public int UserId { get; set; }
        public virtual User User { get; set; }

        public string Privacy { get; set; } // e.g., "Public", "Private"

        public virtual ICollection<Like> Likes { get; set; }
        public virtual ICollection<Comment> Comments { get; set; }

        public Post()
        {
            Privacy = "Public"; // Default value
        }
    }
}
