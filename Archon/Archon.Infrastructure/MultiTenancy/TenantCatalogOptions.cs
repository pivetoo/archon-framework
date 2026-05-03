namespace Archon.Infrastructure.MultiTenancy
{
    public sealed class TenantCatalogOptions
    {
        public string ConnectionString { get; init; } = string.Empty;

        public string ApplicationId { get; init; } = string.Empty;

        public TimeSpan CacheTtl { get; init; } = TimeSpan.FromMinutes(5);

        public bool IsConfigured => !string.IsNullOrWhiteSpace(ConnectionString);
    }
}
