namespace WebChat_Online_MVC.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddCreatedByIdToGroup : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Groups", "CreatedById", c => c.Int(nullable: false));
        }
        
        public override void Down()
        {
            DropColumn("dbo.Groups", "CreatedById");
        }
    }
}
