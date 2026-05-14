namespace Archon.Infrastructure.MultiTenancy
{
    public sealed class IdentityCatalogOptions
    {
        public string BaseUrl { get; init; } = string.Empty;

        public string IntegrationSecret { get; init; } = string.Empty;

        public string ApplicationId { get; init; } = string.Empty;

        public TimeSpan CacheTtl { get; init; } = TimeSpan.FromMinutes(5);

        public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

        public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(IntegrationSecret);
    }
}
