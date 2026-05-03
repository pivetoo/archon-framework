using FluentMigrator;

namespace Archon.Infrastructure.Migrations
{
    [Migration(2)]
    public sealed class Migration_002_CreateIntegrationTables : Migration
    {
        public override void Up()
        {
            Create.Table("integrations")
                .WithColumn("id").AsInt64().PrimaryKey().Identity()
                .WithColumn("name").AsString(150).NotNullable()
                .WithColumn("baseurl").AsString(2000).NotNullable()
                .WithColumn("isactive").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("createdat").AsDateTimeOffset().NotNullable()
                .WithColumn("updatedat").AsDateTimeOffset().Nullable();

            Create.Index("ux_integrations_name")
                .OnTable("integrations")
                .OnColumn("name").Ascending()
                .WithOptions().Unique();

            Create.Table("integrationparameters")
                .WithColumn("id").AsInt64().PrimaryKey().Identity()
                .WithColumn("integrationid").AsInt64().NotNullable()
                .WithColumn("key").AsString(200).NotNullable()
                .WithColumn("value").AsString(int.MaxValue).NotNullable()
                .WithColumn("issecret").AsBoolean().NotNullable().WithDefaultValue(false)
                .WithColumn("isactive").AsBoolean().NotNullable().WithDefaultValue(true)
                .WithColumn("createdat").AsDateTimeOffset().NotNullable()
                .WithColumn("updatedat").AsDateTimeOffset().Nullable();

            Create.Index("ix_integrationparameters_integrationid")
                .OnTable("integrationparameters")
                .OnColumn("integrationid").Ascending();

            Create.Index("ux_integrationparameters_integrationid_key")
                .OnTable("integrationparameters")
                .OnColumn("integrationid").Ascending()
                .OnColumn("key").Ascending()
                .WithOptions().Unique();

            Create.ForeignKey("fk_integrationparameters_integrations_integrationid")
                .FromTable("integrationparameters").ForeignColumn("integrationid")
                .ToTable("integrations").PrimaryColumn("id")
                .OnDelete(System.Data.Rule.Cascade);
        }

        public override void Down()
        {
            Delete.ForeignKey("fk_integrationparameters_integrations_integrationid").OnTable("integrationparameters");

            Delete.Index("ux_integrationparameters_integrationid_key").OnTable("integrationparameters");
            Delete.Index("ix_integrationparameters_integrationid").OnTable("integrationparameters");
            Delete.Table("integrationparameters");

            Delete.Index("ux_integrations_name").OnTable("integrations");
            Delete.Table("integrations");
        }
    }
}
