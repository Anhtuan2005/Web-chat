namespace Online_chat.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class FixFriendshipForeignKeys : DbMigration
    {
        public override void Up()
        {

            DropForeignKey("dbo.Friendships", "SenderId", "dbo.Users");
            DropForeignKey("dbo.Friendships", "ReceiverId", "dbo.Users");
            DropIndex("dbo.Friendships", new[] { "SenderId" });
            DropIndex("dbo.Friendships", new[] { "ReceiverId" });

            AddColumn("dbo.Friendships", "SenderId_New", c => c.Int(nullable: false));
            AddColumn("dbo.Friendships", "ReceiverId_New", c => c.Int(nullable: false));

            Sql(@"
                UPDATE f
                SET f.SenderId_New = f.SenderId,
                    f.ReceiverId_New = f.ReceiverId
                FROM dbo.Friendships f
            ");

            DropColumn("dbo.Friendships", "SenderId");
            DropColumn("dbo.Friendships", "ReceiverId");

            RenameColumn("dbo.Friendships", "SenderId_New", "SenderId");
            RenameColumn("dbo.Friendships", "ReceiverId_New", "ReceiverId");


            CreateIndex("dbo.Friendships", "SenderId");
            CreateIndex("dbo.Friendships", "ReceiverId");
            AddForeignKey("dbo.Friendships", "SenderId", "dbo.Users", "Id", cascadeDelete: false);
            AddForeignKey("dbo.Friendships", "ReceiverId", "dbo.Users", "Id", cascadeDelete: false);


            AddColumn("dbo.Friendships", "RequestedAt", c => c.DateTime(nullable: false, defaultValueSql: "GETDATE()"));
            AddColumn("dbo.Friendships", "RespondedAt", c => c.DateTime());
        }

        public override void Down()
        {
            DropColumn("dbo.Friendships", "RespondedAt");
            DropColumn("dbo.Friendships", "RequestedAt");

            DropForeignKey("dbo.Friendships", "ReceiverId", "dbo.Users");
            DropForeignKey("dbo.Friendships", "SenderId", "dbo.Users");
            DropIndex("dbo.Friendships", new[] { "ReceiverId" });
            DropIndex("dbo.Friendships", new[] { "SenderId" });
        }
    }
}