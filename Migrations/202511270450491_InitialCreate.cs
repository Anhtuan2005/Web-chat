namespace WebChat_Online_MVC.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.BlockedUsers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        BlockerId = c.Int(nullable: false),
                        BlockedId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.BlockedId)
                .ForeignKey("dbo.Users", t => t.BlockerId)
                .Index(t => new { t.BlockerId, t.BlockedId }, unique: true, name: "IX_Blocker_Blocked");
            
            CreateTable(
                "dbo.Users",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Username = c.String(nullable: false, maxLength: 50),
                        DisplayName = c.String(nullable: false, maxLength: 100),
                        Email = c.String(nullable: false, maxLength: 100),
                        PasswordHash = c.String(nullable: false),
                        PhoneNumber = c.String(),
                        AvatarUrl = c.String(),
                        AvatarVersion = c.Long(nullable: false),
                        CoverPhotoUrl = c.String(),
                        Gender = c.String(),
                        DateOfBirth = c.DateTime(),
                        Bio = c.String(),
                        IsAdmin = c.Boolean(nullable: false),
                        IsDeleted = c.Boolean(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        UserCode = c.String(maxLength: 50),
                        FullName = c.String(),
                        PasswordResetToken = c.String(),
                        ResetTokenExpiration = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id);
            
            CreateTable(
                "dbo.Comments",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Content = c.String(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        PostId = c.Int(nullable: false),
                        UserId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Posts", t => t.PostId, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.UserId)
                .Index(t => t.PostId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.Posts",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Content = c.String(),
                        MediaUrl = c.String(),
                        MediaType = c.String(),
                        PostBackground = c.String(),
                        PostType = c.String(),
                        CreatedAt = c.DateTime(nullable: false),
                        UserId = c.Int(nullable: false),
                        Privacy = c.String(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.UserId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.Likes",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        PostId = c.Int(nullable: false),
                        UserId = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Posts", t => t.PostId, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.UserId)
                .Index(t => t.PostId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.GroupMembers",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        GroupId = c.Int(nullable: false),
                        UserId = c.Int(nullable: false),
                        Role = c.String(maxLength: 20),
                        JoinedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Groups", t => t.GroupId, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.UserId)
                .Index(t => t.GroupId)
                .Index(t => t.UserId);
            
            CreateTable(
                "dbo.Groups",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        Name = c.String(nullable: false, maxLength: 100),
                        CreatedById = c.Int(nullable: false),
                        AvatarUrl = c.String(),
                        CreatedBy = c.String(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        OwnerId = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.OwnerId)
                .Index(t => t.OwnerId);
            
            CreateTable(
                "dbo.GroupMessages",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        GroupId = c.Int(nullable: false),
                        SenderId = c.Int(nullable: false),
                        Content = c.String(nullable: false),
                        Timestamp = c.DateTime(nullable: false),
                        IsDeleted = c.Boolean(nullable: false),
                        EditedAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Groups", t => t.GroupId, cascadeDelete: true)
                .ForeignKey("dbo.Users", t => t.SenderId)
                .Index(t => t.GroupId)
                .Index(t => t.SenderId);
            
            CreateTable(
                "dbo.UserPrivacySettings",
                c => new
                    {
                        Id = c.Int(nullable: false),
                        UserId = c.Int(nullable: false),
                        IsProfilePublic = c.Boolean(nullable: false),
                        CanReceiveMessages = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.Id)
                .Index(t => t.Id);
            
            CreateTable(
                "dbo.Friendships",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SenderId = c.Int(nullable: false),
                        ReceiverId = c.Int(nullable: false),
                        Status = c.Int(nullable: false),
                        CreatedAt = c.DateTime(nullable: false),
                        AcceptedAt = c.DateTime(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.ReceiverId)
                .ForeignKey("dbo.Users", t => t.SenderId)
                .Index(t => t.SenderId)
                .Index(t => t.ReceiverId);
            
            CreateTable(
                "dbo.PrivateMessages",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SenderId = c.Int(nullable: false),
                        ReceiverId = c.Int(nullable: false),
                        Content = c.String(nullable: false),
                        Timestamp = c.DateTime(nullable: false),
                        IsRead = c.Boolean(nullable: false),
                        ReadAt = c.DateTime(),
                        DeliveredAt = c.DateTime(),
                        MessageType = c.String(maxLength: 20),
                        Status = c.Int(nullable: false),
                        EditedAt = c.DateTime(),
                        IsDeleted = c.Boolean(nullable: false),
                        DeletedAt = c.DateTime(),
                        ParentMessageId = c.Int(),
                        ForwardedFromId = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.ForwardedFromId)
                .ForeignKey("dbo.Users", t => t.ReceiverId)
                .ForeignKey("dbo.PrivateMessages", t => t.ParentMessageId)
                .ForeignKey("dbo.Users", t => t.SenderId)
                .Index(t => t.SenderId)
                .Index(t => t.ReceiverId)
                .Index(t => t.ParentMessageId)
                .Index(t => t.ForwardedFromId);
            
            CreateTable(
                "dbo.MessageReactions",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        MessageId = c.Int(nullable: false),
                        UserId = c.Int(nullable: false),
                        Emoji = c.String(nullable: false, maxLength: 50),
                        CreatedAt = c.DateTime(nullable: false),
                        PrivateMessage_Id = c.Int(),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.PrivateMessages", t => t.MessageId)
                .ForeignKey("dbo.Users", t => t.UserId)
                .ForeignKey("dbo.PrivateMessages", t => t.PrivateMessage_Id)
                .Index(t => t.MessageId)
                .Index(t => t.UserId)
                .Index(t => t.PrivateMessage_Id);
            
            CreateTable(
                "dbo.HiddenConversations",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.String(nullable: false, maxLength: 128),
                        PartnerUsername = c.String(nullable: false, maxLength: 256),
                    })
                .PrimaryKey(t => t.Id)
                .Index(t => new { t.UserId, t.PartnerUsername }, unique: true, name: "IX_UserAndPartner");
            
            CreateTable(
                "dbo.PinnedConversations",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        UserId = c.Int(nullable: false),
                        ConversationId = c.String(nullable: false, maxLength: 128),
                        ConversationType = c.String(nullable: false, maxLength: 50),
                        PinnedAt = c.DateTime(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Users", t => t.UserId, cascadeDelete: true)
                .Index(t => new { t.UserId, t.ConversationId, t.ConversationType }, unique: true, name: "IX_UserAndConversation");
            
            CreateTable(
                "dbo.Reports",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ReporterId = c.Int(nullable: false),
                        ReportedUserId = c.Int(nullable: false),
                        PostId = c.Int(),
                        Reason = c.String(nullable: false, maxLength: 500),
                        CreatedAt = c.DateTime(nullable: false),
                        IsResolved = c.Boolean(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Posts", t => t.PostId)
                .ForeignKey("dbo.Users", t => t.ReportedUserId)
                .ForeignKey("dbo.Users", t => t.ReporterId)
                .Index(t => t.ReporterId)
                .Index(t => t.ReportedUserId)
                .Index(t => t.PostId);
            
            CreateTable(
                "dbo.Settings",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        SiteName = c.String(nullable: false, maxLength: 100),
                        AllowNewRegistrations = c.Boolean(nullable: false),
                        MaintenanceMessage = c.String(maxLength: 500),
                        IsMaintenanceMode = c.Boolean(nullable: false),
                        MaxUploadSizeMB = c.Int(nullable: false),
                        AdminEmail = c.String(maxLength: 200),
                    })
                .PrimaryKey(t => t.Id);
            
        }
        
        public override void Down()
        {
            DropForeignKey("dbo.Reports", "ReporterId", "dbo.Users");
            DropForeignKey("dbo.Reports", "ReportedUserId", "dbo.Users");
            DropForeignKey("dbo.Reports", "PostId", "dbo.Posts");
            DropForeignKey("dbo.PinnedConversations", "UserId", "dbo.Users");
            DropForeignKey("dbo.BlockedUsers", "BlockerId", "dbo.Users");
            DropForeignKey("dbo.BlockedUsers", "BlockedId", "dbo.Users");
            DropForeignKey("dbo.PrivateMessages", "SenderId", "dbo.Users");
            DropForeignKey("dbo.PrivateMessages", "ParentMessageId", "dbo.PrivateMessages");
            DropForeignKey("dbo.PrivateMessages", "ReceiverId", "dbo.Users");
            DropForeignKey("dbo.MessageReactions", "PrivateMessage_Id", "dbo.PrivateMessages");
            DropForeignKey("dbo.MessageReactions", "UserId", "dbo.Users");
            DropForeignKey("dbo.MessageReactions", "MessageId", "dbo.PrivateMessages");
            DropForeignKey("dbo.PrivateMessages", "ForwardedFromId", "dbo.Users");
            DropForeignKey("dbo.Friendships", "SenderId", "dbo.Users");
            DropForeignKey("dbo.Friendships", "ReceiverId", "dbo.Users");
            DropForeignKey("dbo.UserPrivacySettings", "Id", "dbo.Users");
            DropForeignKey("dbo.GroupMembers", "UserId", "dbo.Users");
            DropForeignKey("dbo.GroupMembers", "GroupId", "dbo.Groups");
            DropForeignKey("dbo.Groups", "OwnerId", "dbo.Users");
            DropForeignKey("dbo.GroupMessages", "SenderId", "dbo.Users");
            DropForeignKey("dbo.GroupMessages", "GroupId", "dbo.Groups");
            DropForeignKey("dbo.Comments", "UserId", "dbo.Users");
            DropForeignKey("dbo.Comments", "PostId", "dbo.Posts");
            DropForeignKey("dbo.Posts", "UserId", "dbo.Users");
            DropForeignKey("dbo.Likes", "UserId", "dbo.Users");
            DropForeignKey("dbo.Likes", "PostId", "dbo.Posts");
            DropIndex("dbo.Reports", new[] { "PostId" });
            DropIndex("dbo.Reports", new[] { "ReportedUserId" });
            DropIndex("dbo.Reports", new[] { "ReporterId" });
            DropIndex("dbo.PinnedConversations", "IX_UserAndConversation");
            DropIndex("dbo.HiddenConversations", "IX_UserAndPartner");
            DropIndex("dbo.MessageReactions", new[] { "PrivateMessage_Id" });
            DropIndex("dbo.MessageReactions", new[] { "UserId" });
            DropIndex("dbo.MessageReactions", new[] { "MessageId" });
            DropIndex("dbo.PrivateMessages", new[] { "ForwardedFromId" });
            DropIndex("dbo.PrivateMessages", new[] { "ParentMessageId" });
            DropIndex("dbo.PrivateMessages", new[] { "ReceiverId" });
            DropIndex("dbo.PrivateMessages", new[] { "SenderId" });
            DropIndex("dbo.Friendships", new[] { "ReceiverId" });
            DropIndex("dbo.Friendships", new[] { "SenderId" });
            DropIndex("dbo.UserPrivacySettings", new[] { "Id" });
            DropIndex("dbo.GroupMessages", new[] { "SenderId" });
            DropIndex("dbo.GroupMessages", new[] { "GroupId" });
            DropIndex("dbo.Groups", new[] { "OwnerId" });
            DropIndex("dbo.GroupMembers", new[] { "UserId" });
            DropIndex("dbo.GroupMembers", new[] { "GroupId" });
            DropIndex("dbo.Likes", new[] { "UserId" });
            DropIndex("dbo.Likes", new[] { "PostId" });
            DropIndex("dbo.Posts", new[] { "UserId" });
            DropIndex("dbo.Comments", new[] { "UserId" });
            DropIndex("dbo.Comments", new[] { "PostId" });
            DropIndex("dbo.BlockedUsers", "IX_Blocker_Blocked");
            DropTable("dbo.Settings");
            DropTable("dbo.Reports");
            DropTable("dbo.PinnedConversations");
            DropTable("dbo.HiddenConversations");
            DropTable("dbo.MessageReactions");
            DropTable("dbo.PrivateMessages");
            DropTable("dbo.Friendships");
            DropTable("dbo.UserPrivacySettings");
            DropTable("dbo.GroupMessages");
            DropTable("dbo.Groups");
            DropTable("dbo.GroupMembers");
            DropTable("dbo.Likes");
            DropTable("dbo.Posts");
            DropTable("dbo.Comments");
            DropTable("dbo.Users");
            DropTable("dbo.BlockedUsers");
        }
    }
}
