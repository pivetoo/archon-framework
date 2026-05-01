using Archon.Core.Access;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Json;

namespace Archon.Infrastructure.IdentityManagement
{
    public sealed class IdentityManagementClient
    {
        private const string OpenIdConfigurationCacheKey = "IdentityManagement:OidcConfiguration";
        private const string SigningKeysCacheKey = "IdentityManagement:SigningKeys";

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

        public async Task<OpenIdConnectConfigurationInfo?> GetOpenIdConfigurationAsync(CancellationToken cancellationToken = default)
        {
            if (cache.TryGetValue(OpenIdConfigurationCacheKey, out OpenIdConnectConfigurationInfo? cachedConfiguration))
            {
                return cachedConfiguration;
            }

            try
            {
                OpenIdConnectConfigurationInfo? configuration = await httpClient.GetFromJsonAsync<OpenIdConnectConfigurationInfo>(
                    "/.well-known/openid-configuration",
                    cancellationToken);

                if (configuration is not null && !string.IsNullOrWhiteSpace(configuration.JwksUri))
                {
                    cache.Set(OpenIdConfigurationCacheKey, configuration, clientLookupCacheTtl);
                }

                return configuration;
            }
            catch
            {
                return null;
            }
        }

        public async Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default)
        {
            if (cache.TryGetValue(SigningKeysCacheKey, out IReadOnlyCollection<SecurityKey>? cachedKeys) && cachedKeys is not null)
            {
                return cachedKeys;
            }

            OpenIdConnectConfigurationInfo? configuration = await GetOpenIdConfigurationAsync(cancellationToken);
            if (configuration is null || string.IsNullOrWhiteSpace(configuration.JwksUri))
            {
                return [];
            }

            try
            {
                string jwks = await httpClient.GetStringAsync(configuration.JwksUri, cancellationToken);
                JsonWebKeySet keySet = new(jwks);
                List<SecurityKey> signingKeys = keySet.Keys.Cast<SecurityKey>().ToList();
                cache.Set(SigningKeysCacheKey, signingKeys, clientLookupCacheTtl);
                return signingKeys;
            }
            catch
            {
                return [];
            }
        }

        public void ClearCache()
        {
            cache.Remove(OpenIdConfigurationCacheKey);
            cache.Remove(SigningKeysCacheKey);
        }

        public async Task SyncAccessResourcesAsync(IReadOnlyCollection<AccessResourceModel> resources, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(resources);

            HttpResponseMessage response = await httpClient.PostAsJsonAsync("/api/AccessResources/Sync", resources, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

    }
}
