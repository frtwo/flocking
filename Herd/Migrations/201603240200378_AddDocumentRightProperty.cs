namespace Herd.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddDocumentRightProperty : DbMigration
    {
        public override void Up()
        {
            AddColumn("dbo.DocumentRights", "DocumentType", c => c.Int(nullable: false, defaultValue: 0));
        }
        
        public override void Down()
        {
            DropColumn("dbo.DocumentRights", "DocumentType");
        }
    }
}
