using System.Net;
using System.Text.Json;
using Archon.Application.Integrations;
using Archon.Application.Services;
using Archon.Core.Access;
using Archon.Infrastructure.IdentityManagement;
using Archon.Infrastructure.Integrations;
using Archon.Infrastructure.MultiTenancy;
using Microsoft.Extensions.Caching.Memory;
using Microsoft.Extensions.Options;
using IntegrationRecord = Archon.Application.Integrations.Integration;
using Rest = Archon.Infrastructure.RestApi.RestApi;

namespace Archon.Testing.Unit.Infrastructure.IdentityManagement
{
    public sealed class IdentityManagementClientAccessSyncTests
    {
        private static readonly AccessResourceModel[] Resources =
        [
            new AccessResourceModel { SystemAudience = "agency-campaign", Name = "brands.get", Controller = "brands", Action = "get", HttpMethod = "GET", Route = "/api/Brands/Get" }
        ];

        [Test]
        public async Task SyncAccessResourcesAsync_ShouldUseCatalogApiKey_WhenIdentityCatalogIsConfigured()
        {
            CapturingHandler handler = new CapturingHandler("{\"message\":\"ok\",\"data\":{\"createdCount\":1,\"updatedCount\":2,\"deactivatedCount\":3,\"totalCount\":4}}");
            IdentityCatalogOptions catalog = new IdentityCatalogOptions { BaseUrl = "https://auth.example.com/", ApiKey = "catalog-key", ApplicationId = "agency-campaign" };
            IdentityManagementClient client = CreateClient(handler, catalog, new ThrowingIntegrationService());

            AccessResourceSyncResult? result = await client.SyncAccessResourcesAsync(Resources);

            Assert.That(handler.Request, Is.Not.Null);
            Assert.That(handler.Request!.RequestUri!.ToString(), Is.EqualTo("https://auth.example.com/api/AccessResources/Sync"));
            Assert.That(handler.Request.Headers.GetValues("X-Api-Key").Single(), Is.EqualTo("catalog-key"));
            Assert.That(handler.Request.Headers.Authorization, Is.Null);
            Assert.That(result, Is.Not.Null);
            Assert.That(result!.CreatedCount, Is.EqualTo(1));
            Assert.That(result.UpdatedCount, Is.EqualTo(2));
            Assert.That(result.DeactivatedCount, Is.EqualTo(3));
            Assert.That(result.TotalCount, Is.EqualTo(4));
        }

        [Test]
        public async Task SyncAccessResourcesAsync_ShouldFallBackToIntegrationTable_WhenIdentityCatalogIsNotConfigured()
        {
            CapturingHandler handler = new CapturingHandler("{\"message\":\"ok\",\"data\":{\"totalCount\":1}}");
            IntegrationRecord integration = new IntegrationRecord
            {
                Name = "identity-management",
                BaseUrl = "https://auth.example.com",
                Parameters =
                [
                    new IntegrationParameter { Key = "TenantId", Value = "tenant-1" },
                    new IntegrationParameter { Key = "ApiKey", Value = "tenant-secret", IsSecret = true }
                ]
            };
            IdentityManagementClient client = CreateClient(handler, new IdentityCatalogOptions(), new FixedIntegrationService(integration));

            AccessResourceSyncResult? result = await client.SyncAccessResourcesAsync(Resources);

            Assert.That(handler.Request, Is.Not.Null);
            Assert.That(handler.Request!.RequestUri!.ToString(), Is.EqualTo("https://auth.example.com/api/AccessResources/Sync"));
            Assert.That(handler.Request.Headers.Authorization?.Scheme, Is.EqualTo("Basic"));
            Assert.That(handler.Request.Headers.Contains("X-Api-Key"), Is.False);
            Assert.That(result?.TotalCount, Is.EqualTo(1));
        }

        [Test]
        public void SyncAccessResourcesAsync_ShouldThrow_WhenIdentityManagementRejectsTheRequest()
        {
            CapturingHandler handler = new CapturingHandler("{\"errors\":[\"unauthorized\"]}", HttpStatusCode.Unauthorized);
            IdentityCatalogOptions catalog = new IdentityCatalogOptions { BaseUrl = "https://auth.example.com", ApiKey = "catalog-key" };
            IdentityManagementClient client = CreateClient(handler, catalog, new ThrowingIntegrationService());

            Assert.ThrowsAsync<HttpRequestException>(() => client.SyncAccessResourcesAsync(Resources));
        }

        private static IdentityManagementClient CreateClient(HttpMessageHandler handler, IdentityCatalogOptions catalog, IIntegrationService integrationService)
        {
            return new IdentityManagementClient(
                new Rest(new HttpClient(handler)),
                new MemoryCache(new MemoryCacheOptions()),
                integrationService,
                Options.Create(new IntegrationOptions()),
                Options.Create(catalog));
        }

        private sealed class CapturingHandler : HttpMessageHandler
        {
            private readonly string responseBody;
            private readonly HttpStatusCode statusCode;

            public CapturingHandler(string responseBody, HttpStatusCode statusCode = HttpStatusCode.OK)
            {
                this.responseBody = responseBody;
                this.statusCode = statusCode;
            }

            public HttpRequestMessage? Request { get; private set; }

            public string? RequestBody { get; private set; }

            protected override async Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
            {
                Request = request;
                RequestBody = request.Content is null ? null : await request.Content.ReadAsStringAsync(cancellationToken);

                Assert.That(RequestBody, Is.Not.Null);
                Assert.That(JsonDocument.Parse(RequestBody!).RootElement.GetArrayLength(), Is.EqualTo(1));

                return new HttpResponseMessage(statusCode)
                {
                    Content = new StringContent(responseBody, System.Text.Encoding.UTF8, "application/json")
                };
            }
        }

        // O caminho pelo catalogo nao pode encostar na tabela integrations: fora de request nao ha
        // tenant resolvido e o DbContext falharia. Qualquer chamada aqui derruba o teste.
        private sealed class ThrowingIntegrationService : IIntegrationService
        {
            public Task<IntegrationRecord?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
            {
                throw new InvalidOperationException("Integration table must not be read when IdentityCatalog is configured.");
            }
        }

        private sealed class FixedIntegrationService : IIntegrationService
        {
            private readonly IntegrationRecord integration;

            public FixedIntegrationService(IntegrationRecord integration)
            {
                this.integration = integration;
            }

            public Task<IntegrationRecord?> GetByNameAsync(string name, CancellationToken cancellationToken = default)
            {
                return Task.FromResult<IntegrationRecord?>(integration);
            }
        }
    }
}
