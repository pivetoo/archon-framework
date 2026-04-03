using FluentMigrator;

namespace Archon.Infrastructure.Migrations
{
    [Migration(1)]
    public sealed class Migration_001_CreateAuditTables : Migration
    {
        public override void Up()
        {
            Create.Table("auditentries")
                .WithColumn("id").AsInt64().PrimaryKey().Identity()
                .WithColumn("entityname").AsString(200).NotNullable()
                .WithColumn("entityid").AsString(100).NotNullable()
                .WithColumn("tenantid").AsString(100).Nullable()
                .WithColumn("action").AsInt32().NotNullable()
                .WithColumn("changedat").AsDateTimeOffset().NotNullable()
                .WithColumn("changedby").AsString(100).Nullable()
                .WithColumn("correlationid").AsString(100).Nullable()
                .WithColumn("parententityname").AsString(200).Nullable()
                .WithColumn("parententityid").AsString(100).Nullable()
                .WithColumn("source").AsString(100).Nullable()
                .WithColumn("createdat").AsDateTimeOffset().NotNullable()
                .WithColumn("updatedat").AsDateTimeOffset().Nullable();

            Create.Index("ix_auditentries_entityname_entityid_changedat")
                .OnTable("auditentries")
                .OnColumn("entityname").Ascending()
                .OnColumn("entityid").Ascending()
                .OnColumn("changedat").Descending();

            Create.Index("ix_auditentries_correlationid")
                .OnTable("auditentries")
                .OnColumn("correlationid").Ascending();

            Create.Table("auditpropertychanges")
                .WithColumn("id").AsInt64().PrimaryKey().Identity()
                .WithColumn("auditentryid").AsInt64().NotNullable()
                .WithColumn("propertyname").AsString(200).NotNullable()
                .WithColumn("oldvalue").AsString(int.MaxValue).Nullable()
                .WithColumn("newvalue").AsString(int.MaxValue).Nullable()
                .WithColumn("createdat").AsDateTimeOffset().NotNullable()
                .WithColumn("updatedat").AsDateTimeOffset().Nullable();

            Create.ForeignKey("fk_auditpropertychanges_auditentries_auditentryid")
                .FromTable("auditpropertychanges").ForeignColumn("auditentryid")
                .ToTable("auditentries").PrimaryColumn("id")
                .OnDelete(System.Data.Rule.Cascade);
        }

        public override void Down()
        {
            Delete.ForeignKey("fk_auditpropertychanges_auditentries_auditentryid").OnTable("auditpropertychanges");

            Delete.Table("auditpropertychanges");

            Delete.Index("ix_auditentries_correlationid").OnTable("auditentries");
            Delete.Index("ix_auditentries_entityname_entityid_changedat").OnTable("auditentries");

            Delete.Table("auditentries");
        }
    }
}
