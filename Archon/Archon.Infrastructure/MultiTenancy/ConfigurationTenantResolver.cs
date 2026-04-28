using System.Collections.Concurrent;
using Archon.Application.MultiTenancy;
using Microsoft.Extensions.Configuration;

namespace Archon.Infrastructure.MultiTenancy
{
    public sealed class ConfigurationTenantResolver : ITenantResolver
    {
        private readonly IConfiguration configuration;
        private readonly ConcurrentDictionary<string, TenantInfo> cache = new(StringComparer.OrdinalIgnoreCase);

        public ConfigurationTenantResolver(IConfiguration configuration)
        {
            this.configuration = configuration;
        }

        public Task<TenantInfo?> ResolveAsync(string? applicationId, CancellationToken cancellationToken = default)
        {
            string cacheKey = string.IsNullOrWhiteSpace(applicationId) ? "default" : applicationId.Trim();

            if (cache.TryGetValue(cacheKey, out TenantInfo? cachedTenant))
            {
                return Task.FromResult<TenantInfo?>(cachedTenant);
            }

            TenantInfo? tenant = ResolveFromConfiguration(applicationId);
            if (tenant is not null)
            {
                cache[cacheKey] = tenant;
            }

            return Task.FromResult<TenantInfo?>(tenant);
        }

        private TenantInfo? ResolveFromConfiguration(string? applicationId)
        {
            IConfigurationSection tenantDatabasesSection = configuration.GetSection("TenantDatabases");
            IEnumerable<IConfigurationSection> tenantSections = tenantDatabasesSection.GetChildren();

            if (string.IsNullOrWhiteSpace(applicationId))
            {
                IConfigurationSection? firstTenant = tenantSections.FirstOrDefault();
                return firstTenant is null ? null : CreateTenantInfo(firstTenant);
            }

            foreach (IConfigurationSection tenantSection in tenantSections)
            {
                string? configuredApplicationId = tenantSection["ApplicationId"];
                if (string.Equals(configuredApplicationId, applicationId, StringComparison.OrdinalIgnoreCase))
                {
                    return CreateTenantInfo(tenantSection);
                }
            }

            return null;
        }

        public Task<TenantInfo?> ResolveBySecretAsync(string? integrationSecret, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(integrationSecret))
            {
                return Task.FromResult<TenantInfo?>(null);
            }

            IConfigurationSection tenantDatabasesSection = configuration.GetSection("TenantDatabases");
            foreach (IConfigurationSection tenantSection in tenantDatabasesSection.GetChildren())
            {
                string? configuredSecret = tenantSection["IntegrationSecret"];
                if (string.Equals(configuredSecret, integrationSecret, StringComparison.Ordinal))
                {
                    return Task.FromResult(CreateTenantInfo(tenantSection));
                }
            }

            return Task.FromResult<TenantInfo?>(null);
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
    }
}
