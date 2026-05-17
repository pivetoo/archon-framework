using System.Net;
using System.Net.Http.Json;
using System.Text.Json;
using System.Text.Json.Serialization;
using Microsoft.Extensions.Options;

namespace Archon.Infrastructure.MultiTenancy
{
    public sealed class IdentityCatalogClient
    {
        private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web);

        private readonly HttpClient httpClient;
        private readonly IdentityCatalogOptions options;

        public IdentityCatalogClient(HttpClient httpClient, IOptions<IdentityCatalogOptions> options)
        {
            this.httpClient = httpClient;
            this.options = options.Value;
            ConfigureHttpClient();
        }

        public async Task<TenantResolutionPayload?> ResolveAsync(string tenantId, string applicationId, CancellationToken cancellationToken)
        {
            string query = $"tenantId={Uri.EscapeDataString(tenantId)}&applicationId={Uri.EscapeDataString(applicationId)}";
            return await SendAsync($"api/Tenants/Resolve?{query}", cancellationToken);
        }

        public async Task<TenantResolutionPayload?> ResolveByApiKeyAsync(string apiKey, string applicationId, CancellationToken cancellationToken)
        {
            string query = $"apiKey={Uri.EscapeDataString(apiKey)}";
            if (!string.IsNullOrWhiteSpace(applicationId))
            {
                query += $"&applicationId={Uri.EscapeDataString(applicationId)}";
            }

            return await SendAsync($"api/Tenants/ResolveByApiKey?{query}", cancellationToken);
        }

        public async Task<IReadOnlyList<TenantResolutionPayload>> ListByApplicationAsync(string applicationId, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, $"api/Tenants/ListByApplication?applicationId={Uri.EscapeDataString(applicationId)}");
            using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return Array.Empty<TenantResolutionPayload>();
            }

            response.EnsureSuccessStatusCode();

            TenantResolutionListEnvelope? envelope = await response.Content.ReadFromJsonAsync<TenantResolutionListEnvelope>(JsonOptions, cancellationToken);
            return envelope?.Data ?? new List<TenantResolutionPayload>();
        }

        private async Task<TenantResolutionPayload?> SendAsync(string relativeUrl, CancellationToken cancellationToken)
        {
            using HttpRequestMessage request = new(HttpMethod.Get, relativeUrl);
            using HttpResponseMessage response = await httpClient.SendAsync(request, HttpCompletionOption.ResponseHeadersRead, cancellationToken);

            if (response.StatusCode == HttpStatusCode.NotFound)
            {
                return null;
            }

            response.EnsureSuccessStatusCode();

            TenantResolutionEnvelope? envelope = await response.Content.ReadFromJsonAsync<TenantResolutionEnvelope>(JsonOptions, cancellationToken);
            return envelope?.Data;
        }

        private void ConfigureHttpClient()
        {
            if (!options.IsConfigured)
            {
                return;
            }

            httpClient.BaseAddress = new Uri(options.BaseUrl.TrimEnd('/') + "/");
            httpClient.Timeout = options.RequestTimeout;
            httpClient.DefaultRequestHeaders.Remove("X-Api-Key");
            httpClient.DefaultRequestHeaders.Add("X-Api-Key", options.IdMApiKey);
        }

        private sealed class TenantResolutionEnvelope
        {
            [JsonPropertyName("data")]
            public TenantResolutionPayload? Data { get; set; }
        }

        private sealed class TenantResolutionListEnvelope
        {
            [JsonPropertyName("data")]
            public List<TenantResolutionPayload>? Data { get; set; }
        }
    }

    public sealed class TenantResolutionPayload
    {
        public string TenantId { get; set; } = string.Empty;

        public string CompanyName { get; set; } = string.Empty;

        public string ApplicationId { get; set; } = string.Empty;

        public string ConnectionString { get; set; } = string.Empty;

        public int DatabaseProvider { get; set; }

        public string Schema { get; set; } = "public";

        public string ApiKey { get; set; } = string.Empty;
    }
}
