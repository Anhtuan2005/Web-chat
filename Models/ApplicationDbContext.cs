using System.Data.Entity;

namespace Online_chat.Models
{
    public class ApplicationDbContext : DbContext
    {
        public ApplicationDbContext() : base("DefaultConnection")
        {
        }
        public DbSet<PrivateMessage> PrivateMessages { get; set; }
        public DbSet<User> Users { get; set; }
        public DbSet<Message> Messages { get; set; }
        public DbSet<Group> Groups { get; set; }
        public DbSet<Friendship> Friendships { get; set; }
        public DbSet<Setting> Settings { get; set; }

        protected override void OnModelCreating(DbModelBuilder modelBuilder)
        {
            base.OnModelCreating(modelBuilder);

            // GROUP
            modelBuilder.Entity<Group>()
                .HasRequired(g => g.Owner)
                .WithMany()
                .WillCascadeOnDelete(false);

            // MESSAGE
            modelBuilder.Entity<Message>()
                .HasRequired(m => m.Sender)
                .WithMany()
                .WillCascadeOnDelete(false);

            // PRIVATE MESSAGE
            modelBuilder.Entity<PrivateMessage>()
                .HasRequired(pm => pm.Sender)
                .WithMany()
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<PrivateMessage>()
                .HasRequired(pm => pm.Receiver)
                .WithMany()
                .WillCascadeOnDelete(false);

            // FRIENDSHIP
            modelBuilder.Entity<Friendship>()
                .HasRequired(f => f.Sender)
                .WithMany()
                .WillCascadeOnDelete(false);

            modelBuilder.Entity<Friendship>()
                .HasRequired(f => f.Receiver)
                .WithMany()
                .WillCascadeOnDelete(false);
        }

    }
}