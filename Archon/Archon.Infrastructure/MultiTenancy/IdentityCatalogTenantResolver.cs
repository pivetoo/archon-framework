using System.Collections.Concurrent;
using System.Security.Cryptography;
using System.Text;
using Archon.Application.MultiTenancy;
using Archon.Core.ValueObjects;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.Options;

namespace Archon.Infrastructure.MultiTenancy
{
    public sealed class IdentityCatalogTenantResolver : ITenantResolver
    {
        private readonly IdentityCatalogClient catalogClient;
        private readonly ConfigurationTenantResolver configurationFallback;
        private readonly IMemoryCache cache;
        private readonly IdentityCatalogOptions options;
        private readonly string currentApplicationId;
        private readonly ConcurrentDictionary<string, SemaphoreSlim> locks = new(StringComparer.OrdinalIgnoreCase);

        public IdentityCatalogTenantResolver(IdentityCatalogClient catalogClient, ConfigurationTenantResolver configurationFallback, IMemoryCache cache, IOptions<IdentityCatalogOptions> options, IConfiguration configuration)
        {
            this.catalogClient = catalogClient;
            this.configurationFallback = configurationFallback;
            this.cache = cache;
            this.options = options.Value;
            currentApplicationId = !string.IsNullOrWhiteSpace(this.options.ApplicationId)
                ? this.options.ApplicationId
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
                    ?? await configurationFallback.ResolveAsync(tenantId, cancellationToken);

                if (tenant is not null)
                {
                    cache.Set(cacheKey, tenant, options.CacheTtl);
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

        public async Task<TenantInfo?> ResolveByApiKeyAsync(string? apiKey, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                return null;
            }

            string normalizedApiKey = apiKey.Trim();
            string cacheKey = $"tenant:apikey:{currentApplicationId}:{normalizedApiKey}";

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

                TenantInfo? tenant = await ResolveByApiKeyFromCatalogAsync(normalizedApiKey, cancellationToken)
                    ?? await configurationFallback.ResolveByApiKeyAsync(normalizedApiKey, cancellationToken);

                if (tenant is not null)
                {
                    cache.Set(cacheKey, tenant, options.CacheTtl);
                }

                return tenant;
            }
            finally
            {
                syncLock.Release();
            }
        }

        private async Task<TenantInfo?> ResolveFromCatalogAsync(string? tenantId, CancellationToken cancellationToken)
        {
            if (!options.IsConfigured || string.IsNullOrWhiteSpace(tenantId) || string.IsNullOrWhiteSpace(currentApplicationId))
            {
                return null;
            }

            try
            {
                TenantResolutionPayload? payload = await catalogClient.ResolveAsync(tenantId.Trim(), currentApplicationId, cancellationToken);
                return ToTenantInfo(payload);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                return null;
            }
        }

        private async Task<TenantInfo?> ResolveByApiKeyFromCatalogAsync(string apiKey, CancellationToken cancellationToken)
        {
            if (!options.IsConfigured)
            {
                return null;
            }

            try
            {
                TenantResolutionPayload? payload = await catalogClient.ResolveByApiKeyAsync(apiKey, currentApplicationId, cancellationToken);
                return ToTenantInfo(payload);
            }
            catch (Exception exception) when (exception is HttpRequestException or TaskCanceledException)
            {
                return null;
            }
        }

        private static TenantInfo? ToTenantInfo(TenantResolutionPayload? payload)
        {
            if (payload is null || string.IsNullOrWhiteSpace(payload.ConnectionString))
            {
                return null;
            }

            return new TenantInfo
            {
                TenantId = payload.TenantId,
                CompanyName = payload.CompanyName,
                ApplicationId = payload.ApplicationId,
                ConnectionString = payload.ConnectionString,
                Schema = string.IsNullOrWhiteSpace(payload.Schema) ? "public" : payload.Schema,
                DatabaseProvider = MapDatabaseProvider(payload.DatabaseProvider),
                ApiKey = payload.IntegrationSecret
            };
        }

        private static DatabaseProvider MapDatabaseProvider(int value)
        {
            return Enum.IsDefined(typeof(DatabaseProvider), value)
                ? (DatabaseProvider)value
                : DatabaseProvider.PostgreSql;
        }
    }
}
