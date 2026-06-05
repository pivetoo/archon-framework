using Archon.Application.Integrations;
using Archon.Application.MultiTenancy;
using Archon.Core.ValueObjects;
using Archon.Infrastructure.DependencyInjection;
using Archon.Infrastructure.Migrations;
using Archon.Infrastructure.MultiTenancy;
using Microsoft.Data.SqlClient;
using MySqlConnector;
using Npgsql;
using System.Data.Common;

namespace Archon.Infrastructure.Integrations
{
    public interface ITenantBootstrapService
    {
        Task<TenantBootstrapResult> BootstrapAsync(TenantBootstrapRequest request, CancellationToken cancellationToken = default);
    }

    public sealed class TenantBootstrapService : ITenantBootstrapService
    {
        private readonly ITenantContext tenantContext;
        private readonly TenantDatabaseOptions tenantDatabaseOptions;
        private readonly TenantMigrationRunner tenantMigrationRunner;

        public TenantBootstrapService(ITenantContext tenantContext, TenantDatabaseOptions tenantDatabaseOptions, TenantMigrationRunner tenantMigrationRunner)
        {
            this.tenantContext = tenantContext;
            this.tenantDatabaseOptions = tenantDatabaseOptions;
            this.tenantMigrationRunner = tenantMigrationRunner;
        }

        public async Task<TenantBootstrapResult> BootstrapAsync(TenantBootstrapRequest request, CancellationToken cancellationToken = default)
        {
            (string connectionString, DatabaseProvider databaseProvider, string? schema) =
                ServiceCollectionExtensions.ResolveCurrentTenant(tenantContext, tenantDatabaseOptions);

            tenantMigrationRunner.Migrate(connectionString, databaseProvider, schema);

            int integrationsSeeded = await SeedIntegrationsAsync(request, connectionString, databaseProvider, schema, cancellationToken);

            return new TenantBootstrapResult
            {
                Migrated = true,
                IntegrationsSeeded = integrationsSeeded
            };
        }

        private async Task<int> SeedIntegrationsAsync(TenantBootstrapRequest request, string connectionString, DatabaseProvider databaseProvider, string? schema, CancellationToken cancellationToken)
        {
            if (request.Integrations.Count == 0)
            {
                return 0;
            }

            await using DbConnection connection = CreateConnection(connectionString, databaseProvider);
            await connection.OpenAsync(cancellationToken);

            int seeded = 0;
            foreach (TenantBootstrapIntegration integration in request.Integrations)
            {
                if (string.IsNullOrWhiteSpace(integration.Name))
                {
                    continue;
                }

                long integrationId = await UpsertIntegrationAsync(connection, integration, schema, cancellationToken);

                foreach (TenantBootstrapParameter parameter in integration.Parameters)
                {
                    if (string.IsNullOrWhiteSpace(parameter.Key))
                    {
                        continue;
                    }

                    await UpsertParameterAsync(connection, integrationId, parameter, schema, cancellationToken);
                }

                seeded++;
            }

            return seeded;
        }

        private static async Task<long> UpsertIntegrationAsync(DbConnection connection, TenantBootstrapIntegration integration, string? schema, CancellationToken cancellationToken)
        {
            await using DbCommand updateCommand = connection.CreateCommand();
            updateCommand.CommandText =
                $"""
                update {GetIntegrationsTableName(schema)}
                set baseurl = @baseurl,
                    isactive = true,
                    updatedat = @now
                where name = @name
                """;

            AddParameter(updateCommand, "@baseurl", integration.BaseUrl);
            AddParameter(updateCommand, "@now", DateTimeOffset.UtcNow);
            AddParameter(updateCommand, "@name", integration.Name.Trim());

            int affected = await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            if (affected == 0)
            {
                await using DbCommand insertCommand = connection.CreateCommand();
                insertCommand.CommandText =
                    $"""
                    insert into {GetIntegrationsTableName(schema)} (name, baseurl, isactive, createdat, updatedat)
                    values (@name, @baseurl, true, @now, @now)
                    """;

                AddParameter(insertCommand, "@name", integration.Name.Trim());
                AddParameter(insertCommand, "@baseurl", integration.BaseUrl);
                AddParameter(insertCommand, "@now", DateTimeOffset.UtcNow);

                await insertCommand.ExecuteNonQueryAsync(cancellationToken);
            }

            await using DbCommand idCommand = connection.CreateCommand();
            idCommand.CommandText =
                $"""
                select id
                from {GetIntegrationsTableName(schema)}
                where name = @name
                """;

            AddParameter(idCommand, "@name", integration.Name.Trim());

            object? result = await idCommand.ExecuteScalarAsync(cancellationToken);
            return Convert.ToInt64(result);
        }

        private static async Task UpsertParameterAsync(DbConnection connection, long integrationId, TenantBootstrapParameter parameter, string? schema, CancellationToken cancellationToken)
        {
            await using DbCommand updateCommand = connection.CreateCommand();
            updateCommand.CommandText =
                $"""
                update {GetIntegrationParametersTableName(schema)}
                set value = @value,
                    issecret = @issecret,
                    isactive = true,
                    updatedat = @now
                where integrationid = @integrationid
                  and key = @key
                """;

            AddParameter(updateCommand, "@value", parameter.Value);
            AddParameter(updateCommand, "@issecret", parameter.IsSecret);
            AddParameter(updateCommand, "@now", DateTimeOffset.UtcNow);
            AddParameter(updateCommand, "@integrationid", integrationId);
            AddParameter(updateCommand, "@key", parameter.Key.Trim());

            int affected = await updateCommand.ExecuteNonQueryAsync(cancellationToken);
            if (affected > 0)
            {
                return;
            }

            await using DbCommand insertCommand = connection.CreateCommand();
            insertCommand.CommandText =
                $"""
                insert into {GetIntegrationParametersTableName(schema)} (integrationid, key, value, issecret, isactive, createdat, updatedat)
                values (@integrationid, @key, @value, @issecret, true, @now, @now)
                """;

            AddParameter(insertCommand, "@integrationid", integrationId);
            AddParameter(insertCommand, "@key", parameter.Key.Trim());
            AddParameter(insertCommand, "@value", parameter.Value);
            AddParameter(insertCommand, "@issecret", parameter.IsSecret);
            AddParameter(insertCommand, "@now", DateTimeOffset.UtcNow);

            await insertCommand.ExecuteNonQueryAsync(cancellationToken);
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
