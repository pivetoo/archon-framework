using Archon.Application.MultiTenancy;
using Archon.Application.Persistence;
using Archon.Core.ValueObjects;
using Archon.Infrastructure.MultiTenancy;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using System.Data.Common;

namespace Archon.Infrastructure.Persistence.Dapper
{
    public sealed class TenantSqlConnectionFactory : ISqlConnectionFactory
    {
        private readonly ITenantContext tenantContext;
        private readonly TenantDatabaseOptions tenantDatabaseOptions;

        public TenantSqlConnectionFactory(ITenantContext tenantContext, TenantDatabaseOptions tenantDatabaseOptions)
        {
            this.tenantContext = tenantContext;
            this.tenantDatabaseOptions = tenantDatabaseOptions;
        }

        public DbConnection CreateConnection()
        {
            (string connectionString, DatabaseProvider databaseProvider) = ResolveCurrentTenantConnection();

            return databaseProvider switch
            {
                DatabaseProvider.PostgreSql => new NpgsqlConnection(connectionString),
                DatabaseProvider.SqlServer => new SqlConnection(connectionString),
                DatabaseProvider.MySql => new MySqlConnection(connectionString),
                _ => throw new ArgumentOutOfRangeException(nameof(databaseProvider), databaseProvider, "Unsupported database provider.")
            };
        }

        public async Task<DbConnection> OpenConnectionAsync(CancellationToken cancellationToken = default)
        {
            DbConnection connection = CreateConnection();
            await connection.OpenAsync(cancellationToken);
            return connection;
        }

        private (string connectionString, DatabaseProvider databaseProvider) ResolveCurrentTenantConnection()
        {
            if (!string.IsNullOrWhiteSpace(tenantContext.ConnectionString))
            {
                return (tenantContext.ConnectionString, tenantContext.DatabaseProvider);
            }

            KeyValuePair<string, TenantDatabaseOption> fallbackTenant = tenantDatabaseOptions.TenantDatabases
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Value.ConnectionString));

            if (!string.IsNullOrWhiteSpace(fallbackTenant.Value?.ConnectionString))
            {
                return (fallbackTenant.Value.ConnectionString, fallbackTenant.Value.GetDatabaseProvider());
            }

            throw new InvalidOperationException("No tenant connection string was configured for the current request.");
        }
    }
}
