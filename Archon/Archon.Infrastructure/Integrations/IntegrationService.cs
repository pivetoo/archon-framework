using Archon.Application.Integrations;
using Archon.Application.MultiTenancy;
using Archon.Application.Services;
using Archon.Core.ValueObjects;
using Archon.Infrastructure.MultiTenancy;
using Microsoft.Data.SqlClient;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using MySqlConnector;
using Npgsql;
using System.Data.Common;

namespace Archon.Infrastructure.Integrations
{
    public sealed class IntegrationService : IIntegrationService
    {
        private readonly ITenantContext tenantContext;
        private readonly IMemoryCache cache;
        private readonly IntegrationOptions options;
        private readonly TenantDatabaseOptions tenantDatabaseOptions;

        public IntegrationService(ITenantContext tenantContext, IMemoryCache cache, IOptions<IntegrationOptions> options, TenantDatabaseOptions tenantDatabaseOptions)
        {
            this.tenantContext = tenantContext;
            this.cache = cache;
            this.options = options.Value;
            this.tenantDatabaseOptions = tenantDatabaseOptions;
        }

        public async Task<Integration?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(name))
            {
                return null;
            }

            string? tenantId = tenantContext.TenantId;
            string cacheKey = $"integration:{tenantId}:{name.Trim().ToLowerInvariant()}";
            if (cache.TryGetValue(cacheKey, out Integration? cachedIntegration))
            {
                return cachedIntegration;
            }

            (string connectionString, DatabaseProvider databaseProvider, string? schema) = ResolveTenant();
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return null;
            }

            await using DbConnection connection = CreateConnection(connectionString, databaseProvider);
            await connection.OpenAsync(cancellationToken);

            Integration? integration = await LoadIntegrationAsync(connection, name.Trim(), schema, cancellationToken);
            if (integration is not null)
            {
                cache.Set(cacheKey, integration, options.CacheTtl);
            }

            return integration;
        }

        private async Task<Integration?> LoadIntegrationAsync(DbConnection connection, string name, string? schema, CancellationToken cancellationToken)
        {
            await using DbCommand integrationCommand = connection.CreateCommand();
            integrationCommand.CommandText =
                $"""
                select
                    id,
                    name,
                    baseurl
                from {GetIntegrationsTableName(schema)}
                where isactive = @isactive
                  and name = @name
                """;

            AddParameter(integrationCommand, "@isactive", true);
            AddParameter(integrationCommand, "@name", name);

            await using DbDataReader integrationReader = await integrationCommand.ExecuteReaderAsync(cancellationToken);
            if (!await integrationReader.ReadAsync(cancellationToken))
            {
                return null;
            }

            long integrationId = integrationReader.GetInt64(0);
            string integrationName = integrationReader.GetString(1);
            string baseUrl = integrationReader.GetString(2);
            await integrationReader.DisposeAsync();

            List<IntegrationParameter> parameters = await LoadParametersAsync(connection, integrationId, schema, cancellationToken);

            return new Integration
            {
                Name = integrationName,
                BaseUrl = baseUrl,
                Parameters = parameters
            };
        }

        private async Task<List<IntegrationParameter>> LoadParametersAsync(DbConnection connection, long integrationId, string? schema, CancellationToken cancellationToken)
        {
            await using DbCommand parametersCommand = connection.CreateCommand();
            parametersCommand.CommandText =
                $"""
                select
                    key,
                    value,
                    coalesce(issecret, false) as issecret
                from {GetIntegrationParametersTableName(schema)}
                where isactive = @isactive
                  and integrationid = @integrationid
                order by id
                """;

            AddParameter(parametersCommand, "@isactive", true);
            AddParameter(parametersCommand, "@integrationid", integrationId);

            List<IntegrationParameter> parameters = new();
            await using DbDataReader parametersReader = await parametersCommand.ExecuteReaderAsync(cancellationToken);
            while (await parametersReader.ReadAsync(cancellationToken))
            {
                parameters.Add(new IntegrationParameter
                {
                    Key = parametersReader.GetString(0),
                    Value = parametersReader.GetString(1),
                    IsSecret = parametersReader.GetBoolean(2)
                });
            }

            return parameters;
        }

        private (string connectionString, DatabaseProvider databaseProvider, string? schema) ResolveTenant()
        {
            if (!string.IsNullOrWhiteSpace(tenantContext.ConnectionString))
            {
                return (tenantContext.ConnectionString, tenantContext.DatabaseProvider, tenantContext.Schema);
            }

            KeyValuePair<string, TenantDatabaseOption> fallbackTenant = tenantDatabaseOptions.TenantDatabases
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Value.ConnectionString));

            if (!string.IsNullOrWhiteSpace(fallbackTenant.Value?.ConnectionString))
            {
                return (
                    fallbackTenant.Value.ConnectionString,
                    fallbackTenant.Value.GetDatabaseProvider(),
                    fallbackTenant.Value.Schema);
            }

            return (string.Empty, DatabaseProvider.PostgreSql, null);
        }

        private static DbConnection CreateConnection(string connectionString, DatabaseProvider databaseProvider)
        {
            return databaseProvider switch
            {
                DatabaseProvider.PostgreSql => new NpgsqlConnection(connectionString),
                DatabaseProvider.SqlServer => new SqlConnection(connectionString),
                DatabaseProvider.MySql => new MySqlConnection(connectionString),
                _ => throw new ArgumentOutOfRangeException(nameof(databaseProvider), databaseProvider, "Unsupported database provider.")
            };
        }

        private static string GetIntegrationsTableName(string? schema)
        {
            return $"{GetSchemaPrefix(schema)}integrations";
        }

        private static string GetIntegrationParametersTableName(string? schema)
        {
            return $"{GetSchemaPrefix(schema)}integrationparameters";
        }

        private static string GetSchemaPrefix(string? schema)
        {
            return string.IsNullOrWhiteSpace(schema) ? string.Empty : $"{schema}.";
        }

        private static void AddParameter(DbCommand command, string name, object? value)
        {
            DbParameter parameter = command.CreateParameter();
            parameter.ParameterName = name;
            parameter.Value = value ?? DBNull.Value;
            command.Parameters.Add(parameter);
        }
    }
}
