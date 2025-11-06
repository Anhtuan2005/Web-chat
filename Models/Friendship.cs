namespace Online_chat.Models
{
    public class Friendship
    {
        public int Id { get; set; }

        public int SenderId { get; set; }
        public virtual User Sender { get; set; }

        public int ReceiverId { get; set; }
        public virtual User Receiver { get; set; }
        public FriendshipStatus Status { get; set; }
    }
}