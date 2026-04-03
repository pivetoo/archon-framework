namespace Archon.Infrastructure.IdentityManagement
{
    public sealed class IdentityManagementOptions
    {
        public string Authority { get; init; } = string.Empty;

        public string IntegrationSecret { get; init; } = string.Empty;
    }
}
