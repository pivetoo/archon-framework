using Archon.Core.ValueObjects;
using System.Collections.Concurrent;
using System.Reflection;

namespace Archon.Infrastructure.Migrations
{
    public sealed class TenantMigrationGuard
    {
        private readonly Assembly[] migrationAssemblies;
        private readonly ConcurrentDictionary<string, bool> migrated = new();
        private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new();

        public TenantMigrationGuard(Assembly[] migrationAssemblies)
        {
            this.migrationAssemblies = migrationAssemblies;
        }

        public void EnsureMigrated(string connectionString, DatabaseProvider databaseProvider, string? schema)
        {
            if (migrated.ContainsKey(connectionString))
            {
                return;
            }

            SemaphoreSlim gate = locks.GetOrAdd(connectionString, _ => new SemaphoreSlim(1, 1));
            gate.Wait();

            try
            {
                if (migrated.ContainsKey(connectionString))
                {
                    return;
                }

                string targetSchema = string.IsNullOrWhiteSpace(schema) ? "public" : schema;
                DatabaseMigrator.Run(connectionString, targetSchema, databaseProvider, migrationAssemblies);
                migrated[connectionString] = true;
            }
            finally
            {
                gate.Release();
            }
        }
    }
}
