using Archon.Core.ValueObjects;
using System.Reflection;

namespace Archon.Infrastructure.Migrations
{
    public sealed class TenantMigrationRunner
    {
        private readonly string defaultSchema;
        private readonly Assembly[] migrationAssemblies;

        public TenantMigrationRunner(string schema, Assembly[] migrationAssemblies)
        {
            this.defaultSchema = schema;
            this.migrationAssemblies = migrationAssemblies;
        }

        public void Migrate(string connectionString, DatabaseProvider databaseProvider, string? schema)
        {
            string targetSchema = schema ?? (string.IsNullOrWhiteSpace(defaultSchema) ? "public" : defaultSchema);

            DatabaseMigrator.Run(connectionString, targetSchema, databaseProvider, migrationAssemblies);
        }
    }
}
