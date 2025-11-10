namespace WebChat_Online_MVC.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class AddRequestedAtRespondedAtToFriendship : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Friendships", "RequestedAt", c => c.DateTime(nullable: false, defaultValueSql: "GETDATE()"));
            AddColumn("dbo.Friendships", "RespondedAt", c => c.DateTime());
        }

        public override void Down()
        {
            DropColumn("dbo.Friendships", "RespondedAt");
            DropColumn("dbo.Friendships", "RequestedAt");
        }
    }
}