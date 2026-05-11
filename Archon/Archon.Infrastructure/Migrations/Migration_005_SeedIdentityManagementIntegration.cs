using FluentMigrator;

namespace Archon.Infrastructure.Migrations
{
    [Migration(5)]
    public sealed class Migration_005_SeedIdentityManagementIntegration : Migration
    {
        public override void Up()
        {
            Execute.Sql(@"
                INSERT INTO integrations (name, baseurl, isactive, createdat, updatedat)
                SELECT 'identity-management', 'https://auth.mainstay.com.br', true, NOW(), NOW()
                WHERE NOT EXISTS (
                    SELECT 1 FROM integrations WHERE name = 'identity-management'
                );

                INSERT INTO integrationparameters (integrationid, key, value, issecret, isactive, createdat, updatedat)
                SELECT i.id, 'ApiKey', '623f90c4-51d3-4b8a-b612-2e3f15b7a9b7', true, true, NOW(), NOW()
                FROM integrations i
                WHERE i.name = 'identity-management'
                AND NOT EXISTS (
                    SELECT 1 FROM integrationparameters ip
                    WHERE ip.integrationid = i.id AND ip.key = 'ApiKey'
                );
            ");
        }

        public override void Down()
        {
            Execute.Sql(@"
                DELETE FROM integrationparameters
                WHERE integrationid = (SELECT id FROM integrations WHERE name = 'identity-management');

                DELETE FROM integrations WHERE name = 'identity-management';
            ");
        }
    }
}
