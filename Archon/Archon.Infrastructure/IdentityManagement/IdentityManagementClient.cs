using Archon.Application.Integrations;
using Archon.Application.Services;
using Archon.Core.Access;
using Archon.Core.Responses;
using Archon.Infrastructure.Integrations;
using Archon.Infrastructure.MultiTenancy;
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
        private readonly IdentityCatalogOptions catalogOptions;
        private readonly TimeSpan cacheTtl;

        public IdentityManagementClient(Rest restApi, IMemoryCache cache, IIntegrationService integrationService, IOptions<IntegrationOptions> options, IOptions<IdentityCatalogOptions> catalogOptions)
        {
            this.restApi = restApi;
            this.cache = cache;
            this.integrationService = integrationService;
            this.catalogOptions = catalogOptions.Value;

            IntegrationOptions integrationOptions = options.Value;
            cacheTtl = integrationOptions.CacheTtl > TimeSpan.Zero
                ? integrationOptions.CacheTtl
                : TimeSpan.FromMinutes(5);
        }

        public async Task<OpenIdConnectConfigurationInfo?> GetOpenIdConfigurationAsync(CancellationToken ct = default)
        {
            return await GetOpenIdConfigurationAsync(null, ct);
        }

        /// <summary>
        /// Descoberta OIDC com autoridade explicita. Passando <paramref name="authority"/>, a busca NAO
        /// toca no banco do tenant — e o que permite validar o token antes de resolver o tenant.
        /// </summary>
        public async Task<OpenIdConnectConfigurationInfo?> GetOpenIdConfigurationAsync(string? authority, CancellationToken ct = default)
        {
            string? baseUrl = !string.IsNullOrWhiteSpace(authority)
                ? authority.TrimEnd('/')
                : await ResolveBaseUrlAsync(ct);

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
            return await GetSigningKeysAsync(null, ct);
        }

        public async Task<IReadOnlyCollection<SecurityKey>> GetSigningKeysAsync(string? authority, CancellationToken ct = default)
        {
            OpenIdConnectConfigurationInfo? config = await GetOpenIdConfigurationAsync(authority, ct);
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

        public async Task<AccessResourceSyncResult?> SyncAccessResourcesAsync(IReadOnlyCollection<AccessResourceModel> resources, CancellationToken ct = default)
        {
            ArgumentNullException.ThrowIfNull(resources);

            RestRequest request = await CreateAccessSyncRequestAsync(resources, ct);
            RestResponse<ApiResponse<AccessResourceSyncResult>> response = await restApi.Fetch<ApiResponse<AccessResourceSyncResult>>(request, ct);

            if (!response.Ok)
            {
                throw new HttpRequestException($"IdentityManagement Sync returned {response.Status}");
            }

            return response.Data?.Data;
        }

        // O sync do catalogo de endpoints e uma operacao do SISTEMA, nao de um tenant, e roda na subida
        // (sem request, logo sem tenant resolvido). Com IdentityCatalog configurado ele autentica pela
        // chave de catalogo e nao toca no banco de tenant nenhum; a tabela integrations fica so como
        // fallback dos sistemas single-tenant que nao usam o catalogo.
        private async Task<RestRequest> CreateAccessSyncRequestAsync(IReadOnlyCollection<AccessResourceModel> resources, CancellationToken ct)
        {
            if (catalogOptions.IsConfigured)
            {
                return RestRequest.Post($"{catalogOptions.BaseUrl.TrimEnd('/')}/api/AccessResources/Sync", resources)
                    .WithApiKey(catalogOptions.ResolvedApiKey);
            }

            (string? baseUrl, string? tenantId, string? secret) = await ResolveIntegrationAsync(ct);
            if (baseUrl is null)
            {
                throw new InvalidOperationException("Integration 'identity-management' is not configured.");
            }

            return RestRequest.Post($"{baseUrl}/api/AccessResources/Sync", resources)
                .WithTenantApiKey(tenantId, secret!);
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

        private async Task<(string? baseUrl, string? tenantId, string? apiKey)> ResolveIntegrationAsync(CancellationToken ct)
        {
            Integration? integration = await integrationService.GetByNameAsync(IntegrationName, ct);
            if (integration is null)
            {
                Console.WriteLine("IdentityManagementClient: integration 'identity-management' was not found in table 'integrations'.");
                return (null, null, null);
            }

            if (string.IsNullOrWhiteSpace(integration.BaseUrl))
            {
                Console.WriteLine("IdentityManagementClient: integration 'identity-management' is configured without baseurl.");
                return (null, null, null);
            }

            string? tenantId = integration.GetParameter("TenantId");
            string? apiKey = integration.GetParameter("ApiKey");
            if (string.IsNullOrWhiteSpace(apiKey))
            {
                Console.WriteLine("IdentityManagementClient: integration 'identity-management' is configured without ApiKey.");
            }

            return (integration.BaseUrl, tenantId, apiKey);
        }
    }
}
