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

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

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
        }
    }
}