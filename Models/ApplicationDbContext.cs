using System.Data.Entity;

namespace Online_chat.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext() : base("DefaultConnection")
        {
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<GroupMessage> GroupMessages { get; set; }
        public DbSet<PrivateMessage> PrivateMessages { get; set; }
        public DbSet<Friendship> Friendships { get; set; }
        public DbSet<Setting> Settings { get; set; }
        public DbSet<MessageReaction> MessageReactions { get; set; }
        public DbSet<HiddenConversation> HiddenConversations { get; set; }
        public DbSet<PinnedConversation> PinnedConversations { get; set; }
        public DbSet<Report> Reports { get; set; }
        public DbSet<Post> Posts { get; set; }
        public DbSet<Like> Likes { get; set; }
        public DbSet<Comment> Comments { get; set; }
        public DbSet<UserPrivacySettings> UserPrivacySettings { get; set; }

        public DbSet<BlockedUser> BlockedUsers { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);
            modelBuilder.Entity<User>()
           .HasOptional(u => u.PrivacySettings)
           .WithRequired(ps => ps.User);
            // Cấu hình relationship cho GroupMessage
            modelBuilder.Entity<GroupMessage>()
                .HasRequired(m => m.Sender)
                .WithMany(u => u.SentGroupMessages)
                .HasForeignKey(m => m.SenderId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<GroupMessage>()
                .HasRequired(m => m.Group)
                .WithMany(g => g.Messages)
                .HasForeignKey(m => m.GroupId)
                .WillCascadeOnDelete(true);

            // Cấu hình relationship cho PrivateMessage
            modelBuilder.Entity<PrivateMessage>()
                .HasRequired(m => m.Sender)
                .WithMany(u => u.SentPrivateMessages)
                .HasForeignKey(m => m.SenderId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PrivateMessage>()
                .HasRequired(m => m.Receiver)
                .WithMany(u => u.ReceivedPrivateMessages)
                .HasForeignKey(m => m.ReceiverId)
                .WillCascadeOnDelete(false);

            // Cấu hình self-relationship cho trả lời tin nhắn
            modelBuilder.Entity<PrivateMessage>()
                .HasMany(m => m.Replies)
                .WithOptional(m => m.ParentMessage)
                .HasForeignKey(m => m.ParentMessageId)
                .WillCascadeOnDelete(false);

            // Cấu hình relationship cho tin nhắn được chuyển tiếp
            modelBuilder.Entity<PrivateMessage>()
                .HasOptional(m => m.ForwardedFrom)
                .WithMany()
                .HasForeignKey(m => m.ForwardedFromId)
                .WillCascadeOnDelete(false);

            // Cấu hình relationship cho Friendship
            modelBuilder.Entity<Friendship>()
                .HasRequired(f => f.Sender)
                .WithMany(u => u.SentFriendRequests)
                .HasForeignKey(f => f.SenderId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Friendship>()
                .HasRequired(f => f.Receiver)
                .WithMany(u => u.ReceivedFriendRequests)
                .HasForeignKey(f => f.ReceiverId)
                .WillCascadeOnDelete(false);

            // Cấu hình relationship cho Group Owner
            modelBuilder.Entity<Group>()
                .HasOptional(g => g.Owner)
                .WithMany()
                .HasForeignKey(g => g.OwnerId)
                .WillCascadeOnDelete(false);

            // Cấu hình relationship cho GroupMember
            modelBuilder.Entity<GroupMember>()
                .HasRequired(gm => gm.User)
                .WithMany(u => u.GroupMembers)
                .HasForeignKey(gm => gm.UserId)
                .WillCascadeOnDelete(false);



            modelBuilder.Entity<GroupMember>()
                .HasRequired(gm => gm.Group)
                .WithMany(g => g.Members)
                .HasForeignKey(gm => gm.GroupId)
                .WillCascadeOnDelete(true);

            // Cấu hình cho MessageReaction
            modelBuilder.Entity<MessageReaction>()
                .HasRequired(r => r.Message)
                .WithMany()
                .HasForeignKey(r => r.MessageId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<MessageReaction>()
                .HasRequired(r => r.User)
                .WithMany()
                .HasForeignKey(r => r.UserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Report>()
            .HasRequired(r => r.Reporter)
            .WithMany()
            .HasForeignKey(r => r.ReporterId)
            .WillCascadeOnDelete(false);

            modelBuilder.Entity<Report>()
                .HasRequired(r => r.ReportedUser)  
                .WithMany()
                .HasForeignKey(r => r.ReportedUserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Report>()
                .HasOptional(r => r.Post) // Report can be for a post or a user
                .WithMany()
                .HasForeignKey(r => r.PostId)
                .WillCascadeOnDelete(false);

            // Cấu hình relationship cho Post
            modelBuilder.Entity<Post>()
                .HasRequired(p => p.User)
                .WithMany(u => u.Posts)
                .HasForeignKey(p => p.UserId)
                .WillCascadeOnDelete(false);

            // Cấu hình relationship cho Like
            modelBuilder.Entity<Like>()
                .HasRequired(l => l.Post)
                .WithMany(p => p.Likes)
                .HasForeignKey(l => l.PostId)
                .WillCascadeOnDelete(true); // Likes should be deleted if the post is deleted

            modelBuilder.Entity<Like>()
                .HasRequired(l => l.User)
                .WithMany(u => u.Likes)
                .HasForeignKey(l => l.UserId)
                .WillCascadeOnDelete(false); // Don't delete user if they unlike something

            // Cấu hình relationship cho Comment
            modelBuilder.Entity<Comment>()
                .HasRequired(c => c.Post)
                .WithMany(p => p.Comments)
                .HasForeignKey(c => c.PostId)
                .WillCascadeOnDelete(true); // Comments should be deleted if the post is deleted

            modelBuilder.Entity<Comment>()
                .HasRequired(c => c.User)
                .WithMany(u => u.Comments)
                .HasForeignKey(c => c.UserId)
                .WillCascadeOnDelete(false); // Don't delete user if they delete a comment
        }
    }
}