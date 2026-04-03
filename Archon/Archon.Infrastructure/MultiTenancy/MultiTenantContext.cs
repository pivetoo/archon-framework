using Archon.Application.MultiTenancy;
using Archon.Core.ValueObjects;

namespace Archon.Infrastructure.MultiTenancy
{
    public sealed class MultiTenantContext : ITenantContext
    {
        public string? TenantId { get; private set; }

        public string? CompanyName { get; private set; }

        public string? ApplicationId { get; private set; }

        public string? ConnectionString { get; private set; }

        public string? Schema { get; private set; }

        public DatabaseProvider DatabaseProvider { get; private set; } = DatabaseProvider.PostgreSql;

        public bool HasTenant => !string.IsNullOrWhiteSpace(TenantId);

        public void SetTenant(TenantInfo tenantInfo)
        {
            ArgumentNullException.ThrowIfNull(tenantInfo);

            TenantId = tenantInfo.TenantId;
            CompanyName = tenantInfo.CompanyName;
            ApplicationId = tenantInfo.ApplicationId;
            ConnectionString = tenantInfo.ConnectionString;
            Schema = tenantInfo.Schema;
            DatabaseProvider = tenantInfo.DatabaseProvider;
        }

        public void Clear()
        {
            TenantId = null;
            CompanyName = null;
            ApplicationId = null;
            ConnectionString = null;
            Schema = null;
            DatabaseProvider = DatabaseProvider.PostgreSql;
        }
    }
}
