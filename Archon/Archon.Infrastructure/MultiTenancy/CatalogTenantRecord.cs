namespace Archon.Infrastructure.MultiTenancy
{
    internal sealed class CatalogTenantRecord
    {
        public string TenantId { get; init; } = string.Empty;

        public string CompanyName { get; init; } = string.Empty;

        public string ApplicationId { get; init; } = string.Empty;

        public string ConnectionString { get; init; } = string.Empty;

        public string DatabaseType { get; init; } = string.Empty;

        public string? Schema { get; init; }

        public string? IntegrationSecret { get; init; }

        public bool IsDefault { get; init; }
    }
}
