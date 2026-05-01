using System.Text.Json.Serialization;

namespace Archon.Infrastructure.IdentityManagement
{
    public sealed class OpenIdConnectConfigurationInfo
    {
        [JsonPropertyName("issuer")]
        public string Issuer { get; init; } = string.Empty;

        [JsonPropertyName("jwks_uri")]
        public string JwksUri { get; init; } = string.Empty;
    }
}
