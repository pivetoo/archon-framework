using FluentMigrator;

namespace Archon.Infrastructure.Migrations
{
    [Migration(1)]
    public sealed class Migration_001_CreateAuditTables : Migration
    {
        public override void Up()
        {
            Create.Table("AuditEntries")
                .WithColumn("Id").AsInt64().PrimaryKey().Identity()
                .WithColumn("EntityName").AsString(200).NotNullable()
                .WithColumn("EntityId").AsString(100).NotNullable()
                .WithColumn("TenantId").AsString(100).Nullable()
                .WithColumn("Action").AsInt32().NotNullable()
                .WithColumn("ChangedAt").AsDateTimeOffset().NotNullable()
                .WithColumn("ChangedBy").AsString(100).Nullable()
                .WithColumn("CorrelationId").AsString(100).Nullable()
                .WithColumn("ParentEntityName").AsString(200).Nullable()
                .WithColumn("ParentEntityId").AsString(100).Nullable()
                .WithColumn("Source").AsString(100).Nullable()
                .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
                .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

            Create.Index("IX_AuditEntries_EntityName_EntityId_ChangedAt")
                .OnTable("AuditEntries")
                .OnColumn("EntityName").Ascending()
                .OnColumn("EntityId").Ascending()
                .OnColumn("ChangedAt").Descending();

            Create.Index("IX_AuditEntries_CorrelationId")
                .OnTable("AuditEntries")
                .OnColumn("CorrelationId").Ascending();

            Create.Table("AuditPropertyChanges")
                .WithColumn("Id").AsInt64().PrimaryKey().Identity()
                .WithColumn("AuditEntryId").AsInt64().NotNullable()
                .WithColumn("PropertyName").AsString(200).NotNullable()
                .WithColumn("OldValue").AsString(int.MaxValue).Nullable()
                .WithColumn("NewValue").AsString(int.MaxValue).Nullable()
                .WithColumn("CreatedAt").AsDateTimeOffset().NotNullable()
                .WithColumn("UpdatedAt").AsDateTimeOffset().Nullable();

            Create.ForeignKey("FK_AuditPropertyChanges_AuditEntries_AuditEntryId")
                .FromTable("AuditPropertyChanges").ForeignColumn("AuditEntryId")
                .ToTable("AuditEntries").PrimaryColumn("Id")
                .OnDelete(System.Data.Rule.Cascade);
        }

        public override void Down()
        {
            Delete.ForeignKey("FK_AuditPropertyChanges_AuditEntries_AuditEntryId").OnTable("AuditPropertyChanges");

            Delete.Table("AuditPropertyChanges");

            Delete.Index("IX_AuditEntries_CorrelationId").OnTable("AuditEntries");
            Delete.Index("IX_AuditEntries_EntityName_EntityId_ChangedAt").OnTable("AuditEntries");

            Delete.Table("AuditEntries");
        }
    }
}
