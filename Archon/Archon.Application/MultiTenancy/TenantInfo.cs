using Archon.Core.ValueObjects;

namespace Archon.Application.MultiTenancy
{
    public sealed class TenantInfo
    {
        public string TenantId { get; init; } = string.Empty;

        public string CompanyName { get; init; } = string.Empty;

        public string ApplicationId { get; init; } = string.Empty;

        public string ConnectionString { get; init; } = string.Empty;

        public string? Schema { get; init; }

        public DatabaseProvider DatabaseProvider { get; init; } = DatabaseProvider.PostgreSql;

        public string? IntegrationSecret { get; init; }
    }
}
