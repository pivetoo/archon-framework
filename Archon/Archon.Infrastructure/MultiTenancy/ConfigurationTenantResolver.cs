using System.Collections.Concurrent;
using Archon.Application.MultiTenancy;
using Archon.Core.ValueObjects;
using Dapper;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;
using Npgsql;
using System.Data.Common;

namespace Archon.Infrastructure.MultiTenancy
{
    public sealed class ConfigurationTenantResolver : ITenantResolver
    {
        private readonly IConfiguration configuration;
        private readonly IMemoryCache cache;
        private readonly TenantCatalogOptions tenantCatalogOptions;
        private readonly string currentApplicationId;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new(StringComparer.OrdinalIgnoreCase);

        public ConfigurationTenantResolver(IConfiguration configuration, IMemoryCache cache, IOptions<TenantCatalogOptions> tenantCatalogOptions)
        {
            this.configuration = configuration;
            this.cache = cache;
            this.tenantCatalogOptions = tenantCatalogOptions.Value;
            currentApplicationId = !string.IsNullOrWhiteSpace(this.tenantCatalogOptions.ApplicationId)
                ? this.tenantCatalogOptions.ApplicationId
                : configuration["Jwt:Audience"] ?? string.Empty;
        }

        public async Task<TenantInfo?> ResolveAsync(string? tenantId, CancellationToken cancellationToken = default)
        {
            string normalizedTenantId = string.IsNullOrWhiteSpace(tenantId) ? "default" : tenantId.Trim();
            string cacheKey = $"tenant:{currentApplicationId}:{normalizedTenantId}";

            if (cache.TryGetValue(cacheKey, out TenantInfo? cachedTenant))
            {
                return cachedTenant;
            }

            SemaphoreSlim syncLock = locks.GetOrAdd(cacheKey, static _ => new SemaphoreSlim(1, 1));
            await syncLock.WaitAsync(cancellationToken);

            try
            {
                if (cache.TryGetValue(cacheKey, out cachedTenant))
                {
                    return cachedTenant;
                }

                TenantInfo? tenant = await ResolveFromCatalogAsync(tenantId, cancellationToken)
                    ?? ResolveFromConfiguration(tenantId);

                if (tenant is not null)
                {
                    cache.Set(cacheKey, tenant, tenantCatalogOptions.CacheTtl);
                }

                return tenant;
            }
            finally
            {
                syncLock.Release();
            }
        }

        private TenantInfo? ResolveFromConfiguration(string? tenantId)
        {
            IConfigurationSection tenantDatabasesSection = configuration.GetSection("TenantDatabases");
            IEnumerable<IConfigurationSection> tenantSections = tenantDatabasesSection.GetChildren();

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                IConfigurationSection? firstTenant = tenantSections.FirstOrDefault();
                return firstTenant is null ? null : CreateTenantInfo(firstTenant);
            }

            foreach (IConfigurationSection tenantSection in tenantSections)
            {
                string? configuredTenantId = tenantSection["TenantId"];
                if (!string.IsNullOrWhiteSpace(configuredTenantId) &&
                    string.Equals(configuredTenantId, tenantId, StringComparison.OrdinalIgnoreCase))
                {
                    return CreateTenantInfo(tenantSection);
                }

                string? configuredApplicationId = tenantSection["ApplicationId"];
                if (string.Equals(configuredApplicationId, tenantId, StringComparison.OrdinalIgnoreCase))
                {
                    return CreateTenantInfo(tenantSection);
                }
            }

            return null;
        }

        public async Task<TenantInfo?> ResolveBySecretAsync(string? integrationSecret, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(integrationSecret))
            {
                return null;
            }

            string cacheKey = $"tenant:secret:{currentApplicationId}:{integrationSecret.Trim()}";
            if (cache.TryGetValue(cacheKey, out TenantInfo? cachedTenant))
            {
                return cachedTenant;
            }

            TenantInfo? tenant = await ResolveBySecretFromCatalogAsync(integrationSecret, cancellationToken);
            if (tenant is not null)
            {
                cache.Set(cacheKey, tenant, tenantCatalogOptions.CacheTtl);
                return tenant;
            }

            IConfigurationSection tenantDatabasesSection = configuration.GetSection("TenantDatabases");
            foreach (IConfigurationSection tenantSection in tenantDatabasesSection.GetChildren())
            {
                string? configuredSecret = tenantSection["IntegrationSecret"];
                if (string.Equals(configuredSecret, integrationSecret, StringComparison.Ordinal))
                {
                    return CreateTenantInfo(tenantSection);
                }
            }

            return null;
        }

        private static TenantInfo? CreateTenantInfo(IConfigurationSection tenantSection)
        {
            string? connectionString = tenantSection["ConnectionString"];
            if (string.IsNullOrWhiteSpace(connectionString))
            {
                return null;
            }

            TenantDatabaseOption option = tenantSection.Get<TenantDatabaseOption>() ?? new TenantDatabaseOption();

            return new TenantInfo
            {
                TenantId = tenantSection.Key,
                CompanyName = option.CompanyName,
                ApplicationId = option.ApplicationId,
                ConnectionString = connectionString,
                Schema = option.Schema,
                DatabaseProvider = option.GetDatabaseProvider(),
                IntegrationSecret = option.IntegrationSecret
            };
        }

        private async Task<TenantInfo?> ResolveFromCatalogAsync(string? tenantId, CancellationToken cancellationToken)
        {
            if (!tenantCatalogOptions.IsConfigured)
            {
                return null;
            }

            await using DbConnection connection = CreateCatalogConnection();
            await connection.OpenAsync(cancellationToken);

            IEnumerable<CatalogTenantRecord> records = await connection.QueryAsync<CatalogTenantRecord>(new CommandDefinition(
                $"""
                select
                    tenantid as TenantId,
                    companyname as CompanyName,
                    applicationid as ApplicationId,
                    connectionstring as ConnectionString,
                    databasetype as DatabaseType,
                    schema as Schema,
                    integrationsecret as IntegrationSecret,
                    coalesce(isdefault, false) as IsDefault
                from {GetCatalogTableName()}
                where isactive = @IsActive
                  and (@ApplicationId = '' or applicationid = @ApplicationId)
                """,
                new
                {
                    IsActive = true,
                    ApplicationId = currentApplicationId
                },
                cancellationToken: cancellationToken));

            CatalogTenantRecord? record = string.IsNullOrWhiteSpace(tenantId)
                ? records
                    .OrderByDescending(item => item.IsDefault)
                    .ThenBy(item => item.TenantId, StringComparer.OrdinalIgnoreCase)
                    .FirstOrDefault()
                : records.FirstOrDefault(item => string.Equals(item.TenantId, tenantId.Trim(), StringComparison.OrdinalIgnoreCase));

            return record is null ? null : CreateTenantInfo(record);
        }

        private async Task<TenantInfo?> ResolveBySecretFromCatalogAsync(string integrationSecret, CancellationToken cancellationToken)
        {
            if (!tenantCatalogOptions.IsConfigured)
            {
                return null;
            }

            await using DbConnection connection = CreateCatalogConnection();
            await connection.OpenAsync(cancellationToken);

            CatalogTenantRecord? record = await connection.QueryFirstOrDefaultAsync<CatalogTenantRecord>(new CommandDefinition(
                $"""
                select
                    tenantid as TenantId,
                    companyname as CompanyName,
                    applicationid as ApplicationId,
                    connectionstring as ConnectionString,
                    databasetype as DatabaseType,
                    schema as Schema,
                    integrationsecret as IntegrationSecret,
                    coalesce(isdefault, false) as IsDefault
                from {GetCatalogTableName()}
                where isactive = @IsActive
                  and integrationsecret = @IntegrationSecret
                  and (@ApplicationId = '' or applicationid = @ApplicationId)
                """,
                new
                {
                    IsActive = true,
                    IntegrationSecret = integrationSecret.Trim(),
                    ApplicationId = currentApplicationId
                },
                cancellationToken: cancellationToken));

            return record is null ? null : CreateTenantInfo(record);
        }

        private TenantInfo? CreateTenantInfo(CatalogTenantRecord record)
        {
            if (string.IsNullOrWhiteSpace(record.ConnectionString))
            {
                return null;
            }

            return new TenantInfo
            {
                TenantId = record.TenantId,
                CompanyName = record.CompanyName,
                ApplicationId = record.ApplicationId,
                ConnectionString = record.ConnectionString,
                Schema = string.IsNullOrWhiteSpace(record.Schema) ? "public" : record.Schema,
                DatabaseProvider = ResolveDatabaseProvider(record.DatabaseType),
                IntegrationSecret = record.IntegrationSecret
            };
        }

        private DbConnection CreateCatalogConnection()
        {
            return new NpgsqlConnection(tenantCatalogOptions.ConnectionString);
        }

        private string GetCatalogTableName()
        {
            return "public.tenantdatabases";
        }

        private static DatabaseProvider ResolveDatabaseProvider(string databaseType)
        {
            return databaseType.Trim().ToLowerInvariant() switch
            {
                "postgresql" or "postgres" => DatabaseProvider.PostgreSql,
                "sqlserver" or "mssql" => DatabaseProvider.SqlServer,
                "mysql" => DatabaseProvider.MySql,
                _ => DatabaseProvider.PostgreSql
            };
        }
    }
}
