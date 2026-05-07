using FluentMigrator;

namespace Archon.Infrastructure.Migrations
{
    [Migration(3)]
    public sealed class Migration_003_CreateNotifications : Migration
    {
        public override void Up()
        {
            Create.Table("notifications")
                .WithColumn("id").AsInt64().PrimaryKey().Identity()
                .WithColumn("userid").AsInt64().Nullable()
                .WithColumn("tenantid").AsString(100).Nullable()
                .WithColumn("title").AsString(200).NotNullable()
                .WithColumn("message").AsString(2000).NotNullable()
                .WithColumn("type").AsInt32().NotNullable().WithDefaultValue(0)
                .WithColumn("isread").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("readat").AsDateTimeOffset().Nullable()
                .WithColumn("link").AsString(500).Nullable()
                .WithColumn("source").AsString(100).Nullable()
                .WithColumn("referenceentityname").AsString(200).Nullable()
                .WithColumn("referenceentityid").AsString(100).Nullable()
                .WithColumn("createdat").AsDateTimeOffset().NotNullable()
                .WithColumn("updatedat").AsDateTimeOffset().Nullable();

            Create.Index("ix_notifications_userid_isread_createdat")
                .OnTable("notifications")
                .OnColumn("userid").Ascending()
                .OnColumn("isread").Ascending()
                .OnColumn("createdat").Descending();

            Create.Index("ix_notifications_tenantid_userid")
                .OnTable("notifications")
                .OnColumn("tenantid").Ascending()
                .OnColumn("userid").Ascending();
        }

        public override void Down()
        {
            Delete.Index("ix_notifications_tenantid_userid").OnTable("notifications");
            Delete.Index("ix_notifications_userid_isread_createdat").OnTable("notifications");
            Delete.Table("notifications");
        }
    }
}
