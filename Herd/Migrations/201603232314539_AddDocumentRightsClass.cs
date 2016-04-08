namespace Herd.Migrations
{
    using System;
    using System.Data.Entity.Migrations;
    
    public partial class AddDocumentRightsClass : DbMigration
    {
        public override void Up()
        {
            CreateTable(
                "dbo.DocumentRights",
                c => new
                {
                    Id = c.Guid(nullable: false, identity: true),
                    UserName = c.String(nullable: false, maxLength: 128),
                    DocumentId = c.String(nullable: false),
                    DocumentType = c.Int(nullable: false, defaultValue: 0),
                })
                .PrimaryKey(t => t.Id)
                .Index(t => t.UserName);
        }
        
        public override void Down()
        {
            DropIndex("dbo.DocumentRights", new[] { "UserName" });
            DropTable("dbo.DocumentRights");
        }
    }
}
