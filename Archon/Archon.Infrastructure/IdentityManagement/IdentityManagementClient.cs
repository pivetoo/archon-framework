using Archon.Application.Integrations;
using Archon.Application.Services;
using Archon.Core.Access;
using Archon.Infrastructure.Integrations;
using Archon.Infrastructure.RestApi;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using Rest = Archon.Infrastructure.RestApi.RestApi;

namespace Archon.Infrastructure.IdentityManagement
{
    public sealed class IdentityManagementClient
    {
        private const string IntegrationName = "identity-management";

        private readonly Rest restApi;
        private readonly IMemoryCache cache;
        private readonly IIntegrationService integrationService;
        private readonly TimeSpan cacheTtl;

        public IdentityManagementClient(Rest restApi, IMemoryCache cache, IIntegrationService integrationService, IOptions<IntegrationOptions> options)
        {
            this.restApi = restApi;
            this.cache = cache;
            this.integrationService = integrationService;

            IntegrationOptions integrationOptions = options.Value;
            cacheTtl = integrationOptions.CacheTtl > TimeSpan.Zero
                ? integrationOptions.CacheTtl
                : TimeSpan.FromMinutes(5);
        }

        public async Task<OpenIdConnectConfigurationInfo?> GetOpenIdConfigurationAsync(CancellationToken ct = default)
        {
            string? baseUrl = await ResolveBaseUrlAsync(ct);
            if (baseUrl is null)
            {
                return null;
            }

            string cacheKey = $"IdentityManagement:{baseUrl}:OidcConfiguration";
            if (cache.TryGetValue(cacheKey, out OpenIdConnectConfigurationInfo? cached) && cached is not null)
            {
                return cached;
            }

            RestResponse<OpenIdConnectConfigurationInfo> response = await restApi.Fetch<OpenIdConnectConfigurationInfo>(
                RestRequest.Get($"{baseUrl}/.well-known/openid-configuration"), ct);

            if (!response.Ok)
            {
                return null;
            }

            if (response.Data is not null)
            {
                cache.Set(cacheKey, response.Data, cacheTtl);
            }

            return response.Data;
        }

        public async Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(CancellationToken ct = default)
        {
            OpenIdConnectConfigurationInfo? config = await GetOpenIdConfigurationAsync(ct);
            if (config is null || string.IsNullOrWhiteSpace(config.JwksUri))
            {
                return [];
            }

            string cacheKey = $"IdentityManagement:{config.JwksUri}:SigningKeys";
            if (cache.TryGetValue(cacheKey, out IReadOnlyCollection<SecurityKey>? cachedKeys) && cachedKeys is not null)
            {
                return cachedKeys;
            }

            RestResponse<string> response = await restApi.FetchString(
                RestRequest.Get(config.JwksUri), ct);

            if (!response.Ok)
            {
                return [];
            }

            JsonWebKeySet keySet = new(response.Data!);
            List<SecurityKey> signingKeys = keySet.Keys.Cast<SecurityKey>().ToList();
            cache.Set(cacheKey, signingKeys, cacheTtl);
            return signingKeys;
        }

        public async Task SyncAccessResourcesAsync(IReadOnlyCollection<AccessResourceModel> resources, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(resources);

            (string? baseUrl, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            RestResponse<object> response = await restApi.Fetch<object>(
                RestRequest.Post($"{baseUrl}/api/AccessResources/Sync", resources)
                           .WithApiKey(secret!), ct);

            if (!response.Ok)
            {
                throw new HttpRequestException($"IdentityManagement Sync returned {response.Status}");
            }
        }

        private async Task<string?> ResolveBaseUrlAsync(CancellationToken ct)
        {
            Integration? integration = await integrationService.GetByNameAsync(IntegrationName, ct);
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

            return integration.BaseUrl;
        }

        private async Task<(string? baseUrl, string? secret)> ResolveIntegrationAsync(CancellationToken ct)
        {
            Integration? integration = await integrationService.GetByNameAsync(IntegrationName, ct);
            if (integration is null)
            {
                Console.WriteLine("IdentityManagementClient: integration 'identity-management' was not found in table 'integrations'.");
                return (null, null);
            }

            if (string.IsNullOrWhiteSpace(integration.BaseUrl))
            {
                Console.WriteLine("IdentityManagementClient: integration 'identity-management' is configured without baseurl.");
                return (null, null);
            }

            string? apiKey = integration.GetParameter("ApiKey") ?? integration.GetParameter("IntegrationSecret");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("IdentityManagementClient: integration 'identity-management' is configured without ApiKey.");
            }

            return (integration.BaseUrl, apiKey);
        }
    }
}
