namespace Archon.Application.Integrations
{
    public sealed class TenantBootstrapResult
    {
        public bool Migrated { get; init; }

        public int IntegrationsSeeded { get; init; }
    }
}
