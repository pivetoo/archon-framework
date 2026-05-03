namespace Archon.Infrastructure.Integrations
{
    public sealed class IntegrationOptions
    {
        public TimeSpan CacheTtl { get; init; } = TimeSpan.FromMinutes(5);
    }
}
