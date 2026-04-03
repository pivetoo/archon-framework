using Archon.Core.ValueObjects;

namespace Archon.Application.MultiTenancy
{
    public interface ITenantContext
    {
        string? TenantId { get; }

        string? CompanyName { get; }

        string? ApplicationId { get; }

        string? ConnectionString { get; }

        string? Schema { get; }

        DatabaseProvider DatabaseProvider { get; }

        bool HasTenant { get; }
    }
}
