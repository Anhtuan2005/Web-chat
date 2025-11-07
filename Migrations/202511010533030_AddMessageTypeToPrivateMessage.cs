namespace Online_chat.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddMessageTypeToPrivateMessage : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PrivateMessages", "MessageType", c => c.String(nullable: false, maxLength: 10));
        }
        
        public override void Down()
        {
            DropColumn("dbo.PrivateMessages", "MessageType");
        }
    }
}
