namespace Archon.Application.Integrations
{
    public sealed class TenantBootstrapRequest
    {
        public List<TenantBootstrapIntegration> Integrations { get; set; } = new();
    }

    public sealed class TenantBootstrapIntegration
    {
        public string Name { get; set; } = string.Empty;

        public string BaseUrl { get; set; } = string.Empty;

        public List<TenantBootstrapParameter> Parameters { get; set; } = new();
    }

    public sealed class TenantBootstrapParameter
    {
        public string Key { get; set; } = string.Empty;

        public string Value { get; set; } = string.Empty;

        public bool IsSecret { get; set; }
    }
}
