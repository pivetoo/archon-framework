namespace Archon.Infrastructure.MultiTenancy
{
    public sealed class IdentityCatalogOptions
    {
        public string BaseUrl { get; init; } = string.Empty;

        public string ApiKey { get; init; } = string.Empty;

        public string ApplicationId { get; init; } = string.Empty;

        public TimeSpan CacheTtl { get; init; } = TimeSpan.FromMinutes(5);

        public TimeSpan NegativeCacheTtl { get; init; } = TimeSpan.FromSeconds(60);

        public TimeSpan RequestTimeout { get; init; } = TimeSpan.FromSeconds(10);

        public bool IsConfigured => !string.IsNullOrWhiteSpace(BaseUrl) && !string.IsNullOrWhiteSpace(ApiKey);
    }
}
