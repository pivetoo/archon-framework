using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Archon.Application.MultiTenancy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Archon.Infrastructure.MultiTenancy
{
    public sealed class ConfigurationTenantResolver : ITenantResolver
    {
        private readonly IConfiguration configuration;
        private readonly IMemoryCache cache;
        private readonly TimeSpan cacheTtl;
        private readonly string currentApplicationId;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new(StringComparer.OrdinalIgnoreCase);

        public ConfigurationTenantResolver(IConfiguration configuration, IMemoryCache cache, IOptions<IdentityCatalogOptions> identityCatalogOptions)
        {
            this.configuration = configuration;
            this.cache = cache;

            IdentityCatalogOptions options = identityCatalogOptions.Value;
            cacheTtl = options.CacheTtl;
            currentApplicationId = !string.IsNullOrWhiteSpace(options.ApplicationId)
                ? options.ApplicationId
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

                TenantInfo? tenant = ResolveFromConfiguration(tenantId);

                if (tenant is not null)
                {
                    cache.Set(cacheKey, tenant, cacheTtl);
                }

                return tenant;
            }
            finally
            {
                syncLock.Release();
            }
        }

        public async Task<TenantInfo?> ResolveByTenantAndApiKeyAsync(string? tenantId, string? apiKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            TenantInfo? tenant = await ResolveAsync(tenantId, cancellationToken);
            if (tenant is null || string.IsNullOrWhiteSpace(tenant.ApiKey))
            {
                return null;
            }

            byte[] expected = Encoding.UTF8.GetBytes(tenant.ApiKey);
            byte[] received = Encoding.UTF8.GetBytes(apiKey);

            return CryptographicOperations.FixedTimeEquals(expected, received) ? tenant : null;
        }

        public Task<TenantInfo?> ResolveByApiKeyAsync(string? apiKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return Task.FromResult<TenantInfo?>(null);
            }

            string normalizedApiKey = apiKey.Trim();
            string cacheKey = $"tenant:apikey:{currentApplicationId}:{normalizedApiKey}";

            if (cache.TryGetValue(cacheKey, out TenantInfo? cachedTenant))
            {
                return Task.FromResult(cachedTenant);
            }

            IConfigurationSection tenantDatabasesSection = configuration.GetSection("TenantDatabases");
            foreach (IConfigurationSection tenantSection in tenantDatabasesSection.GetChildren())
            {
                string? configuredApiKey = tenantSection["ApiKey"] ?? tenantSection["IntegrationSecret"];
                if (string.Equals(configuredApiKey, normalizedApiKey, StringComparison.Ordinal))
                {
                    TenantInfo? tenant = CreateTenantInfo(tenantSection);
                    if (tenant is not null)
                    {
                        cache.Set(cacheKey, tenant, cacheTtl);
                    }

                    return Task.FromResult(tenant);
                }
            }

            return Task.FromResult<TenantInfo?>(null);
        }

        private TenantInfo? ResolveFromConfiguration(string? tenantId)
        {
            if (string.IsNullOrWhiteSpace(tenantId))
            {
                return null;
            }

            IConfigurationSection tenantDatabasesSection = configuration.GetSection("TenantDatabases");
            string normalizedTenantId = tenantId.Trim();

            foreach (IConfigurationSection tenantSection in tenantDatabasesSection.GetChildren())
            {
                if (string.Equals(tenantSection.Key, normalizedTenantId, StringComparison.OrdinalIgnoreCase))
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

            string? apiKey = option.ApiKey;
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                apiKey = tenantSection["IntegrationSecret"];
            }

            return new TenantInfo
            {
                TenantId = tenantSection.Key,
                CompanyName = option.CompanyName,
                ApplicationId = option.ApplicationId,
                ConnectionString = connectionString,
                Schema = string.IsNullOrWhiteSpace(option.Schema) ? "public" : option.Schema,
                DatabaseProvider = option.GetDatabaseProvider(),
                ApiKey = apiKey
            };
        }
    }
}
