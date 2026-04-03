namespace Archon.Infrastructure.IdentityManagement
{
    public sealed class IdentityManagementApplicationInfo
    {
        public long Id { get; init; }

        public string ClientId { get; init; } = string.Empty;

        public string Name { get; init; } = string.Empty;

        public string JwtSecretKey { get; init; } = string.Empty;

        public bool IsActive { get; init; }

        public int AccessTokenLifetime { get; init; }

        public int RefreshTokenLifetime { get; init; }
    }
}
