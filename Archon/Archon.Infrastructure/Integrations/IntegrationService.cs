using Archon.Application.Integrations;
using Archon.Application.MultiTenancy;
using Archon.Application.Services;
using Archon.Core.ValueObjects;
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

        public IntegrationService(ITenantContext tenantContext, IMemoryCache cache, IOptions<IntegrationOptions> options)
        {
            this.tenantContext = tenantContext;
            this.cache = cache;
            this.options = options.Value;
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

            if (string.IsNullOrWhiteSpace(tenantContext.ConnectionString))
            {
                return null;
            }

            await using DbConnection connection = CreateTenantConnection();
            await connection.OpenAsync(cancellationToken);

            Integration? integration = await LoadIntegrationAsync(connection, name.Trim(), cancellationToken);
            if (integration is not null)
            {
                cache.Set(cacheKey, integration, options.CacheTtl);
            }

            return integration;
        }

        private async Task<Integration?> LoadIntegrationAsync(DbConnection connection, string name, CancellationToken cancellationToken)
        {
            await using DbCommand integrationCommand = connection.CreateCommand();
            integrationCommand.CommandText =
                $"""
                select
                    id,
                    name,
                    baseurl
                from {GetIntegrationsTableName()}
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

            List<IntegrationParameter> parameters = await LoadParametersAsync(connection, integrationId, cancellationToken);

            return new Integration
            {
                Name = integrationName,
                BaseUrl = baseUrl,
                Parameters = parameters
            };
        }

        private async Task<List<IntegrationParameter>> LoadParametersAsync(DbConnection connection, long integrationId, CancellationToken cancellationToken)
        {
            await using DbCommand parametersCommand = connection.CreateCommand();
            parametersCommand.CommandText =
                $"""
                select
                    key,
                    value,
                    coalesce(issecret, false) as issecret
                from {GetIntegrationParametersTableName()}
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

        private DbConnection CreateTenantConnection()
        {
            return tenantContext.DatabaseProvider switch
            {
                DatabaseProvider.PostgreSql => new NpgsqlConnection(tenantContext.ConnectionString),
                DatabaseProvider.SqlServer => new SqlConnection(tenantContext.ConnectionString),
                DatabaseProvider.MySql => new MySqlConnection(tenantContext.ConnectionString),
                _ => throw new ArgumentOutOfRangeException(nameof(tenantContext.DatabaseProvider), tenantContext.DatabaseProvider, "Unsupported database provider.")
            };
        }

        private string GetIntegrationsTableName()
        {
            return $"{GetSchemaPrefix()}integrations";
        }

        private string GetIntegrationParametersTableName()
        {
            return $"{GetSchemaPrefix()}integrationparameters";
        }

        private string GetSchemaPrefix()
        {
            return string.IsNullOrWhiteSpace(tenantContext.Schema) ? string.Empty : $"{tenantContext.Schema}.";
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
