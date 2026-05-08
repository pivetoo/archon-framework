namespace Archon.Infrastructure.IdentityManagement
{
    public sealed class JwtOptions
    {
        public string Issuer { get; init; } = string.Empty;

        public string Audience { get; init; } = string.Empty;
    }
}
