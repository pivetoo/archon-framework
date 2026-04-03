using Archon.Core.ValueObjects;

namespace Archon.Infrastructure.MultiTenancy
{
    public sealed class TenantDatabaseOptions
    {
        public Dictionary<string, TenantDatabaseOption> TenantDatabases { get; init; } = new(StringComparer.OrdinalIgnoreCase);
    }

    public sealed class TenantDatabaseOption
    {
        public string CompanyName { get; init; } = string.Empty;

        public string ApplicationId { get; init; } = string.Empty;

        public string ConnectionString { get; init; } = string.Empty;

        public string DatabaseType { get; init; } = nameof(DatabaseProvider.PostgreSql);

        public string? Schema { get; init; }

        public DatabaseProvider GetDatabaseProvider()
        {
            return DatabaseType.Trim().ToLowerInvariant() switch
            {
                "postgresql" or "postgres" => DatabaseProvider.PostgreSql,
                "sqlserver" or "mssql" => DatabaseProvider.SqlServer,
                "mysql" => DatabaseProvider.MySql,
                _ => DatabaseProvider.PostgreSql
            };
        }
    }
}
