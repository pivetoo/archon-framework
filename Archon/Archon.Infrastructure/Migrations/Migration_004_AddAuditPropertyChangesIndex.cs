using FluentMigrator;

namespace Archon.Infrastructure.Migrations
{
    [Migration(004)]
    public sealed class Migration_004_AddAuditPropertyChangesIndex : Migration
    {
        public override void Up()
        {
            if (!Schema.Table("auditpropertychanges").Index("ixauditpropertychangesauditentryid").Exists())
            {
                Create.Index("ixauditpropertychangesauditentryid")
                    .OnTable("auditpropertychanges")
                    .OnColumn("auditentryid").Ascending();
            }
        }

        public override void Down()
        {
            if (Schema.Table("auditpropertychanges").Index("ixauditpropertychangesauditentryid").Exists())
            {
                Delete.Index("ixauditpropertychangesauditentryid").OnTable("auditpropertychanges");
            }
        }
    }
}
