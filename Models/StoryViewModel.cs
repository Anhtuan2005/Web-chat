using System.Collections.Generic;
using Online_chat.Models;

namespace Online_chat.Models
{
    public class StoryViewModel
    {
        public User User { get; set; }
        public List<Post> Stories { get; set; }
    }
}
