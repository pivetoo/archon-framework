using Archon.Infrastructure.IdentityManagement;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Archon.Api.Security.Authentication
{
    public sealed class DynamicJwtValidator
    {
        private readonly IdentityManagementClient identityManagementClient;
        private readonly JwtOptions jwtOptions;

        public DynamicJwtValidator(IdentityManagementClient identityManagementClient, IOptions<JwtOptions> jwtOptions)
        {
            this.identityManagementClient = identityManagementClient;
            this.jwtOptions = jwtOptions.Value;
        }

        public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            OpenIdConnectConfigurationInfo? configuration = await identityManagementClient.GetOpenIdConfigurationAsync(cancellationToken);
            IReadOnlyCollection<SecurityKey> signingKeys = await identityManagementClient.GetSigningKeysAsync(cancellationToken);

            if (signingKeys.Count == 0)
            {
                return null;
            }

            string issuer = !string.IsNullOrWhiteSpace(jwtOptions.Issuer)
                ? jwtOptions.Issuer
                : configuration?.Issuer ?? string.Empty;

            if (string.IsNullOrWhiteSpace(issuer) || string.IsNullOrWhiteSpace(jwtOptions.Audience))
            {
                return null;
            }

            TokenValidationParameters validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            return tokenHandler.ValidateToken(token, validationParameters, out _);
        }
    }
}
