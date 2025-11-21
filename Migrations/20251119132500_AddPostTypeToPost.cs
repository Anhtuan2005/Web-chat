namespace Online_chat.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddPostTypeToPost : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.Posts", "PostType", c => c.String());
        }
        
        public override void Down()
        {
            DropColumn("dbo.Posts", "PostType");
        }
    }
}
