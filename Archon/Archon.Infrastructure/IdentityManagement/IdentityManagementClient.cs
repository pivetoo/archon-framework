using Archon.Core.Access;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using System.Net.Http.Json;
using System.Collections.Concurrent;

namespace Archon.Infrastructure.IdentityManagement
{
    public sealed class IdentityManagementClient
    {
        private const string ApplicationCacheKeyPrefix = "IdentityManagement:Application:";
        private static readonly ConcurrentDictionary<string, SemaphoreSlim> applicationLocks = new(StringComparer.OrdinalIgnoreCase);
        private static readonly ConcurrentDictionary<string, byte> cachedKeys = new(StringComparer.OrdinalIgnoreCase);

        private readonly HttpClient httpClient;
        private readonly IMemoryCache cache;
        private readonly TimeSpan clientLookupCacheTtl;

        public IdentityManagementClient(HttpClient httpClient, IMemoryCache cache, IOptions<IdentityManagementOptions> options)
        {
            this.httpClient = httpClient;
            this.cache = cache;

            IdentityManagementOptions identityManagementOptions = options.Value;
            if (string.IsNullOrWhiteSpace(identityManagementOptions.Authority))
            {
                throw new InvalidOperationException("IdentityManagement:Authority is not configured.");
            }

            clientLookupCacheTtl = identityManagementOptions.ClientLookupCacheTtl > TimeSpan.Zero
                ? identityManagementOptions.ClientLookupCacheTtl
                : TimeSpan.FromMinutes(5);

            this.httpClient.BaseAddress = new Uri(identityManagementOptions.Authority, UriKind.Absolute);

            if (!string.IsNullOrWhiteSpace(identityManagementOptions.IntegrationSecret))
            {
                this.httpClient.DefaultRequestHeaders.Remove("X-Integration-Secret");
                this.httpClient.DefaultRequestHeaders.Add("X-Integration-Secret", identityManagementOptions.IntegrationSecret);
            }
        }

        public async Task<IdentityManagementApplicationInfo?> GetApplicationByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return null;
            }

            string cacheKey = GetApplicationCacheKey(clientId);
            if (TryGetCachedApplication(cacheKey, out IdentityManagementApplicationInfo? cachedApplication))
            {
                return cachedApplication;
            }

            SemaphoreSlim applicationLock = applicationLocks.GetOrAdd(cacheKey, _ => new SemaphoreSlim(1, 1));

            try
            {
                await applicationLock.WaitAsync(cancellationToken);

                if (TryGetCachedApplication(cacheKey, out cachedApplication))
                {
                    return cachedApplication;
                }

                IdentityManagementApplicationResponse? response = await httpClient.GetFromJsonAsync<IdentityManagementApplicationResponse>($"/api/auth/GetContractByClientId/{clientId}", cancellationToken);
                IdentityManagementApplicationInfo? application = response?.Data;
                if (application is not null && application.IsActive)
                {
                    cache.Set(cacheKey, application, clientLookupCacheTtl);
                    cachedKeys.TryAdd(cacheKey, 0);
                }

                return application;
            }
            catch
            {
                return null;
            }
            finally
            {
                if (applicationLock.CurrentCount == 0)
                {
                    applicationLock.Release();
                }
            }
        }

        public void ClearCache()
        {
            foreach (string cacheKey in cachedKeys.Keys)
            {
                cache.Remove(cacheKey);
                cachedKeys.TryRemove(cacheKey, out _);
            }
        }

        public async Task SyncAccessResourcesAsync(IReadOnlyCollection<AccessResourceModel> resources, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(resources);

            HttpResponseMessage response = await httpClient.PostAsJsonAsync("/api/AccessResources/Sync", resources, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private static string GetApplicationCacheKey(string clientId)
        {
            return string.Concat(ApplicationCacheKeyPrefix, clientId.Trim());
        }

        private bool TryGetCachedApplication(string cacheKey, out IdentityManagementApplicationInfo? application)
        {
            bool found = cache.TryGetValue(cacheKey, out application);
            if (!found)
            {
                cachedKeys.TryRemove(cacheKey, out _);
            }

            return found;
        }
    }
}
