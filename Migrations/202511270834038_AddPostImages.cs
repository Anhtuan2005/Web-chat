namespace WebChat_Online_MVC.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPostImages : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.PostImages",
                c => new
                    {
                        Id = c.Int(nullable: false, identity: true),
                        ImageUrl = c.String(),
                        PostId = c.Int(nullable: false),
                    })
                .PrimaryKey(t => t.Id)
                .ForeignKey("dbo.Posts", t => t.PostId, cascadeDelete: true)
                .Index(t => t.PostId);
            
            DropColumn("dbo.Posts", "MediaUrl");
            DropColumn("dbo.Posts", "MediaType");
        }
        
        public override void Down()
        {
            AddColumn("dbo.Posts", "MediaType", c => c.String());
            AddColumn("dbo.Posts", "MediaUrl", c => c.String());
            DropForeignKey("dbo.PostImages", "PostId", "dbo.Posts");
            DropIndex("dbo.PostImages", new[] { "PostId" });
            DropTable("dbo.PostImages");
        }
    }
}
