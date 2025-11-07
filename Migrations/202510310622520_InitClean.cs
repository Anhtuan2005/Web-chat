namespace Online_chat.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitClean : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.Friendships",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SenderId = c.Int(nullable: false),
                        ReceiverId = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        User_Id = c.Int(),
                        User_Id1 = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.User_Id)
                .ForeignKey("dbo.Users", t => t.User_Id1)
                .ForeignKey("dbo.Users", t => t.ReceiverId)
                .ForeignKey("dbo.Users", t => t.SenderId)
                .Index(t => t.SenderId)
                .Index(t => t.ReceiverId)
                .Index(t => t.User_Id)
                .Index(t => t.User_Id1);
            
            CreateTable(
                "dbo.Users",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserCode = c.String(),
                        Username = c.String(),
                        PasswordHash = c.String(),
                        DisplayName = c.String(),
                        Email = c.String(),
                        PhoneNumber = c.String(),
                        AvatarUrl = c.String(),
                        AvatarVersion = c.Long(nullable: false),
                        CoverPhotoUrl = c.String(),
                        IsAdmin = c.Boolean(nullable: false),
                        IsDeleted = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Messages",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Content = c.String(),
                        Timestamp = c.DateTime(nullable: false),
                        SenderId = c.Int(nullable: false),
                        GroupId = c.Int(nullable: false),
                        User_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Groups", t => t.GroupId, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.SenderId)
                .ForeignKey("dbo.Users", t => t.User_Id)
                .Index(t => t.SenderId)
                .Index(t => t.GroupId)
                .Index(t => t.User_Id);
            
            CreateTable(
                "dbo.Groups",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        GroupName = c.String(),
                        CreatedAt = c.DateTime(nullable: false),
                        OwnerId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.OwnerId)
                .Index(t => t.OwnerId);
            
            CreateTable(
                "dbo.PrivateMessages",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Content = c.String(nullable: false),
                        Timestamp = c.DateTime(nullable: false),
                        SenderId = c.Int(nullable: false),
                        ReceiverId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.ReceiverId)
                .ForeignKey("dbo.Users", t => t.SenderId)
                .Index(t => t.SenderId)
                .Index(t => t.ReceiverId);
            
            CreateTable(
                "dbo.Settings",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SiteName = c.String(),
                        AllowNewRegistrations = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.PrivateMessages", "SenderId", "dbo.Users");
            DropForeignKey("dbo.PrivateMessages", "ReceiverId", "dbo.Users");
            DropForeignKey("dbo.Friendships", "SenderId", "dbo.Users");
            DropForeignKey("dbo.Friendships", "ReceiverId", "dbo.Users");
            DropForeignKey("dbo.Messages", "User_Id", "dbo.Users");
            DropForeignKey("dbo.Messages", "SenderId", "dbo.Users");
            DropForeignKey("dbo.Groups", "OwnerId", "dbo.Users");
            DropForeignKey("dbo.Messages", "GroupId", "dbo.Groups");
            DropForeignKey("dbo.Friendships", "User_Id1", "dbo.Users");
            DropForeignKey("dbo.Friendships", "User_Id", "dbo.Users");
            DropIndex("dbo.PrivateMessages", new[] { "ReceiverId" });
            DropIndex("dbo.PrivateMessages", new[] { "SenderId" });
            DropIndex("dbo.Groups", new[] { "OwnerId" });
            DropIndex("dbo.Messages", new[] { "User_Id" });
            DropIndex("dbo.Messages", new[] { "GroupId" });
            DropIndex("dbo.Messages", new[] { "SenderId" });
            DropIndex("dbo.Friendships", new[] { "User_Id1" });
            DropIndex("dbo.Friendships", new[] { "User_Id" });
            DropIndex("dbo.Friendships", new[] { "ReceiverId" });
            DropIndex("dbo.Friendships", new[] { "SenderId" });
            DropTable("dbo.Settings");
            DropTable("dbo.PrivateMessages");
            DropTable("dbo.Groups");
            DropTable("dbo.Messages");
            DropTable("dbo.Users");
            DropTable("dbo.Friendships");
        }
    }
}
