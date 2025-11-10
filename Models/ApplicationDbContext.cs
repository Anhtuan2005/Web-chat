using Online_chat.Models;
using System.Data.Entity;

namespace Online_chat.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext() : base("DefaultConnection")
        {
            Database.SetInitializer<ApplicationDbContext>(null);
        }

        public DbSet<User> Users { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<PrivateMessage> PrivateMessages { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<GroupMember> GroupMembers { get; set; }
        public DbSet<GroupMessage> GroupMessages { get; set; }
        public DbSet<Friendship> Friendships { get; set; }
        public DbSet<Setting> Settings { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // MESSAGE
            modelBuilder.Entity<Message>()
                .HasRequired(m => m.Sender)
                .WithMany()
                .HasForeignKey(m => m.SenderId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Message>()
                .HasRequired(m => m.Receiver)
                .WithMany()
                .HasForeignKey(m => m.ReceiverId)
                .WillCascadeOnDelete(false);

            // PRIVATE MESSAGE
            modelBuilder.Entity<PrivateMessage>()
                .HasRequired(pm => pm.Sender)
                .WithMany()
                .HasForeignKey(pm => pm.SenderId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PrivateMessage>()
                .HasRequired(pm => pm.Receiver)
                .WithMany()
                .HasForeignKey(pm => pm.ReceiverId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Friendship>()
                .HasRequired(f => f.Sender)
                .WithMany(u => u.InitiatedFriendships)
                .HasForeignKey(f => f.SenderId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Friendship>()
                .HasRequired(f => f.Receiver)
                .WithMany(u => u.ReceivedFriendships)
                .HasForeignKey(f => f.ReceiverId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<GroupMember>()
                .HasRequired(gm => gm.User)
                .WithMany()
                .HasForeignKey(gm => gm.UserId)
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<GroupMessage>()
                .HasRequired(gm => gm.Sender)
                .WithMany()
                .HasForeignKey(gm => gm.SenderId)
                .WillCascadeOnDelete(false);
        }
    }
}