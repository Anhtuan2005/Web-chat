namespace WebChat_Online_MVC.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddMessageStatusAndDeliveredAt : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.PrivateMessages", "DeliveredAt", c => c.DateTime());
        }
        
        public override void Down()
        {
            DropColumn("dbo.PrivateMessages", "DeliveredAt");
        }
    }
}
