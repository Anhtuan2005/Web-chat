namespace WebChat_Online_MVC.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddMissingTables : DbMigration
    {
        public override void Up()
        {
            DropForeignKey("dbo.Messages", "GroupId", "dbo.Groups");
            DropForeignKey("dbo.Messages", "ReceiverId", "dbo.Users");
            DropForeignKey("dbo.Messages", "SenderId", "dbo.Users");
            DropForeignKey("dbo.Messages", "User_Id", "dbo.Users");
            DropIndex("dbo.Messages", new[] { "SenderId" });
            DropIndex("dbo.Messages", new[] { "ReceiverId" });
            DropIndex("dbo.Messages", new[] { "GroupId" });
            DropIndex("dbo.Messages", new[] { "User_Id" });
            DropTable("dbo.Messages");
        }
        
        public override void Down()
        {
            CreateTable(
                "dbo.Messages",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SenderId = c.Int(nullable: false),
                        ReceiverId = c.Int(nullable: false),
                        Content = c.String(nullable: false),
                        Timestamp = c.DateTime(nullable: false),
                        IsRead = c.Boolean(nullable: false),
                        GroupId = c.Int(),
                        User_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateIndex("dbo.Messages", "User_Id");
            CreateIndex("dbo.Messages", "GroupId");
            CreateIndex("dbo.Messages", "ReceiverId");
            CreateIndex("dbo.Messages", "SenderId");
            AddForeignKey("dbo.Messages", "User_Id", "dbo.Users", "Id");
            AddForeignKey("dbo.Messages", "SenderId", "dbo.Users", "Id");
            AddForeignKey("dbo.Messages", "ReceiverId", "dbo.Users", "Id");
            AddForeignKey("dbo.Messages", "GroupId", "dbo.Groups", "Id");
        }
    }
}
