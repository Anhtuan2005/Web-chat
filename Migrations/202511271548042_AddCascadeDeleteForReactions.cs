namespace WebChat_Online_MVC.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCascadeDeleteForReactions : DbMigration
    {
        public override void Up()
        {
            // Drop existing foreign key
            DropForeignKey("dbo.Reactions", "MessageId", "dbo.Messages");

            AddForeignKey("dbo.Reactions", "MessageId", "dbo.Messages", "Id", cascadeDelete: true);

            // Làm tương tự cho ParentMessageId nếu cần
            DropForeignKey("dbo.Messages", "ParentMessageId", "dbo.Messages");
            AddForeignKey("dbo.Messages", "ParentMessageId", "dbo.Messages", "Id", cascadeDelete: false);

            // Và ForwardedFromMessageId
            DropForeignKey("dbo.Messages", "ForwardedFromMessageId", "dbo.Messages");
            AddForeignKey("dbo.Messages", "ForwardedFromMessageId", "dbo.Messages", "Id", cascadeDelete: false);
        }

        public override void Down()
        {
            // Rollback logic
            DropForeignKey("dbo.Reactions", "MessageId", "dbo.Messages");
            AddForeignKey("dbo.Reactions", "MessageId", "dbo.Messages", "Id", cascadeDelete: false);

            DropForeignKey("dbo.Messages", "ParentMessageId", "dbo.Messages");
            AddForeignKey("dbo.Messages", "ParentMessageId", "dbo.Messages", "Id");

            DropForeignKey("dbo.Messages", "ForwardedFromMessageId", "dbo.Messages");
            AddForeignKey("dbo.Messages", "ForwardedFromMessageId", "dbo.Messages", "Id");
        }
    }
}
