namespace WebChat_Online_MVC.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddIsReadToPrivateMessages : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PrivateMessages", "IsRead", c => c.Boolean(nullable: false, defaultValue: false));
        }

        public override void Down()
        {
            DropColumn("dbo.PrivateMessages", "IsRead");
        }
    }
}
