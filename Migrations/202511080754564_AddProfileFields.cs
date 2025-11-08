namespace WebChat_Online_MVC.Migrations
{
    using System;
    using System.Data.Entity.Migrations;

    public partial class AddProfileFields : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Users", "Gender", c => c.String());
            AddColumn("dbo.Users", "DateOfBirth", c => c.DateTime());
            AddColumn("dbo.Users", "Bio", c => c.String());
        }

        public override void Down()
        {
            DropColumn("dbo.Users", "Bio");
            DropColumn("dbo.Users", "DateOfBirth");
            DropColumn("dbo.Users", "Gender");
        }
    }
}