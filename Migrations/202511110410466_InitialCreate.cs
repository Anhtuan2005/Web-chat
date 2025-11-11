namespace WebChat_Online_MVC.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class InitialCreate : DbMigration
    {
        public override void Up()
        {
            DropIndex("dbo.Users", new[] { "Username" });
            DropIndex("dbo.Users", new[] { "Email" });
            DropIndex("dbo.Users", new[] { "UserCode" });
            AddColumn("dbo.Friendships", "CreatedAt", c => c.DateTime(nullable: false));
            AddColumn("dbo.Friendships", "AcceptedAt", c => c.DateTime());
            AddColumn("dbo.PrivateMessages", "Status", c => c.Int(nullable: false));
            AddColumn("dbo.Settings", "MaintenanceMessage", c => c.String(maxLength: 500));
            AddColumn("dbo.Settings", "IsMaintenanceMode", c => c.Boolean(nullable: false));
            AddColumn("dbo.Settings", "MaxUploadSizeMB", c => c.Int(nullable: false));
            AddColumn("dbo.Settings", "AdminEmail", c => c.String(maxLength: 200));
            AlterColumn("dbo.Users", "Username", c => c.String(nullable: false, maxLength: 50));
            AlterColumn("dbo.Users", "Email", c => c.String(nullable: false, maxLength: 100));
            AlterColumn("dbo.Users", "UserCode", c => c.String(maxLength: 50));
            AlterColumn("dbo.PrivateMessages", "MessageType", c => c.String(maxLength: 20));
            AlterColumn("dbo.Settings", "SiteName", c => c.String(nullable: false, maxLength: 100));
            DropColumn("dbo.Friendships", "RequestedAt");
            DropColumn("dbo.Friendships", "RespondedAt");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Friendships", "RespondedAt", c => c.DateTime());
            AddColumn("dbo.Friendships", "RequestedAt", c => c.DateTime(nullable: false));
            AlterColumn("dbo.Settings", "SiteName", c => c.String());
            AlterColumn("dbo.PrivateMessages", "MessageType", c => c.String());
            AlterColumn("dbo.Users", "UserCode", c => c.String(maxLength: 8));
            AlterColumn("dbo.Users", "Email", c => c.String(nullable: false, maxLength: 256));
            AlterColumn("dbo.Users", "Username", c => c.String(nullable: false, maxLength: 256));
            DropColumn("dbo.Settings", "AdminEmail");
            DropColumn("dbo.Settings", "MaxUploadSizeMB");
            DropColumn("dbo.Settings", "IsMaintenanceMode");
            DropColumn("dbo.Settings", "MaintenanceMessage");
            DropColumn("dbo.PrivateMessages", "Status");
            DropColumn("dbo.Friendships", "AcceptedAt");
            DropColumn("dbo.Friendships", "CreatedAt");
            CreateIndex("dbo.Users", "UserCode", unique: true);
            CreateIndex("dbo.Users", "Email", unique: true);
            CreateIndex("dbo.Users", "Username", unique: true);
        }
    }
}
