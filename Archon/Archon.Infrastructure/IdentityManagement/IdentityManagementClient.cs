using Microsoft.Extensions.Options;
using System.Collections.Concurrent;
using System.Net.Http.Json;
using Archon.Core.Access;

namespace Archon.Infrastructure.IdentityManagement
{
    public sealed class IdentityManagementClient
    {
        private readonly HttpClient httpClient;
        private readonly ConcurrentDictionary<string, IdentityManagementApplicationInfo> cache = new(StringComparer.OrdinalIgnoreCase);

        public IdentityManagementClient(HttpClient httpClient, IOptions<IdentityManagementOptions> options)
        {
            this.httpClient = httpClient;

            IdentityManagementOptions identityManagementOptions = options.Value;
            if (string.IsNullOrWhiteSpace(identityManagementOptions.Authority))
            {
                throw new InvalidOperationException("IdentityManagement:Authority is not configured.");
            }

            if (string.IsNullOrWhiteSpace(identityManagementOptions.IntegrationSecret))
            {
                throw new InvalidOperationException("IdentityManagement:IntegrationSecret is not configured.");
            }

            this.httpClient.BaseAddress = new Uri(identityManagementOptions.Authority, UriKind.Absolute);
            this.httpClient.DefaultRequestHeaders.Remove("X-Integration-Secret");
            this.httpClient.DefaultRequestHeaders.Add("X-Integration-Secret", identityManagementOptions.IntegrationSecret);
        }

        public async Task<IdentityManagementApplicationInfo?> GetApplicationByClientIdAsync(string clientId, CancellationToken cancellationToken = default)
        {
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return null;
            }

            if (cache.TryGetValue(clientId, out IdentityManagementApplicationInfo? cachedApplication))
            {
                return cachedApplication;
            }

            try
            {
                IdentityManagementApplicationInfo? application = await httpClient.GetFromJsonAsync<IdentityManagementApplicationInfo>($"/api/auth/contract/{clientId}", cancellationToken);
                if (application is not null && application.IsActive)
                {
                    cache[clientId] = application;
                }

                return application;
            }
            catch
            {
                return null;
            }
        }

        public void ClearCache()
        {
            cache.Clear();
        }

        public async Task SyncAccessResourcesAsync(IReadOnlyCollection<AccessResourceModel> resources, CancellationToken cancellationToken = default)
        {
            ArgumentNullException.ThrowIfNull(resources);

            HttpResponseMessage response = await httpClient.PostAsJsonAsync("/api/access-resources/sync", resources, cancellationToken);
            response.EnsureSuccessStatusCode();
        }
    }
}
