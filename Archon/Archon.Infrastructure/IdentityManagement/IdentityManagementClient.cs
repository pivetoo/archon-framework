using Archon.Application.Integrations;
using Archon.Application.Services;
using Archon.Core.Access;
using Archon.Infrastructure.Integrations;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Net.Http.Json;

namespace Archon.Infrastructure.IdentityManagement
{
    public sealed class IdentityManagementClient
    {
        private const string IdentityManagementIntegrationName = "identity-management";

        private readonly HttpClient httpClient;
        private readonly IMemoryCache cache;
        private readonly TimeSpan clientLookupCacheTtl;
        private readonly IIntegrationService integrationService;

        public IdentityManagementClient(HttpClient httpClient, IMemoryCache cache, IIntegrationService integrationService, IOptions<IntegrationOptions> options)
        {
            this.httpClient = httpClient;
            this.cache = cache;
            this.integrationService = integrationService;

            IntegrationOptions integrationOptions = options.Value;
            clientLookupCacheTtl = integrationOptions.CacheTtl > TimeSpan.Zero
                ? integrationOptions.CacheTtl
                : TimeSpan.FromMinutes(5);
        }

        public async Task<OpenIdConnectConfigurationInfo?> GetOpenIdConfigurationAsync(CancellationToken cancellationToken = default)
        {
            Integration? integration = await EnsureConfiguredAsync(cancellationToken);
            if (integration is null)
            {
                return null;
            }

            string cacheKey = GetOpenIdConfigurationCacheKey(integration.BaseUrl);
            if (cache.TryGetValue(cacheKey, out OpenIdConnectConfigurationInfo? cachedConfiguration))
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
                    cache.Set(cacheKey, configuration, clientLookupCacheTtl);
                }

                return configuration;
            }
            catch
            {
                Console.WriteLine("IdentityManagementClient: failed to load /.well-known/openid-configuration.");
                return null;
            }
        }

        public async Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(CancellationToken cancellationToken = default)
        {
            Integration? integration = await EnsureConfiguredAsync(cancellationToken);
            if (integration is null)
            {
                return [];
            }

            string cacheKey = GetSigningKeysCacheKey(integration.BaseUrl);
            if (cache.TryGetValue(cacheKey, out IReadOnlyCollection<SecurityKey>? cachedKeys) && cachedKeys is not null)
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
                cache.Set(cacheKey, signingKeys, clientLookupCacheTtl);
                return signingKeys;
            }
            catch
            {
                Console.WriteLine("IdentityManagementClient: failed to load JWKS signing keys.");
                return [];
            }
        }

        public void ClearCache()
        {
            // Cache is namespaced by base URL and naturally expires via TTL.
        }

        public async Task SyncAccessResourcesAsync(IReadOnlyCollection<AccessResourceModel> resources, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(resources);
            Integration? integration = await EnsureConfiguredAsync(cancellationToken);
            if (integration is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            HttpResponseMessage response = await httpClient.PostAsJsonAsync("/api/AccessResources/Sync", resources, cancellationToken);
            response.EnsureSuccessStatusCode();
        }

        private async Task<Integration?> EnsureConfiguredAsync(CancellationToken cancellationToken)
        {
            Integration? integration = await integrationService.GetByNameAsync(IdentityManagementIntegrationName, cancellationToken);
            if (integration is null)
            {
                Console.WriteLine("IdentityManagementClient: integration 'identity-management' was not found in table 'integrations'.");
                return null;
            }

            if (string.IsNullOrWhiteSpace(integration.BaseUrl))
            {
                Console.WriteLine("IdentityManagementClient: integration 'identity-management' is configured without baseurl.");
                return null;
            }

            httpClient.BaseAddress = new Uri(integration.BaseUrl, UriKind.Absolute);

            httpClient.DefaultRequestHeaders.Remove("X-Integration-Secret");
            string? integrationSecret = integration.GetParameter("IntegrationSecret");
            if (!string.IsNullOrWhiteSpace(integrationSecret))
            {
                httpClient.DefaultRequestHeaders.Add("X-Integration-Secret", integrationSecret);
            }
            else
            {
                Console.WriteLine("IdentityManagementClient: integration 'identity-management' is configured without IntegrationSecret.");
            }

            return integration;
        }

        private static string GetOpenIdConfigurationCacheKey(string baseUrl)
        {
            return $"IdentityManagement:{baseUrl}:OidcConfiguration";
        }

        private static string GetSigningKeysCacheKey(string baseUrl)
        {
            return $"IdentityManagement:{baseUrl}:SigningKeys";
        }

    }
}
