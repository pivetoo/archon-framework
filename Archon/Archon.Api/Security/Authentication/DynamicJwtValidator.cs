using Archon.Infrastructure.IdentityManagement;
using Microsoft.Extensions.Logging;
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
        private readonly ILogger<DynamicJwtValidator> logger;

        public DynamicJwtValidator(IdentityManagementClient identityManagementClient, IOptions<JwtOptions> jwtOptions, ILogger<DynamicJwtValidator> logger)
        {
            this.identityManagementClient = identityManagementClient;
            this.jwtOptions = jwtOptions.Value;
            this.logger = logger;
        }

        public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler();
            OpenIdConnectConfigurationInfo? configuration = await identityManagementClient.GetOpenIdConfigurationAsync(cancellationToken);
            IReadOnlyCollection<SecurityKey> signingKeys = await identityManagementClient.GetSigningKeysAsync(cancellationToken);

            if (signingKeys.Count == 0)
            {
                logger.LogWarning("JWT validation aborted: no signing keys available.");
                return null;
            }

            string issuer = !string.IsNullOrWhiteSpace(jwtOptions.Issuer)
                ? jwtOptions.Issuer
                : configuration?.Issuer ?? string.Empty;

            List<string> validAudiences = (jwtOptions.Audiences ?? Array.Empty<string>())
                .Where(item => !string.IsNullOrWhiteSpace(item))
                .ToList();
            if (!string.IsNullOrWhiteSpace(jwtOptions.Audience) && !validAudiences.Contains(jwtOptions.Audience))
            {
                validAudiences.Add(jwtOptions.Audience);
            }

            if (string.IsNullOrWhiteSpace(issuer) || validAudiences.Count == 0)
            {
                logger.LogWarning("JWT validation aborted: issuer='{Issuer}' validAudiences={Count}.", issuer, validAudiences.Count);
                return null;
            }

            TokenValidationParameters validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = true,
                ValidIssuer = issuer,
                ValidateAudience = true,
                ValidAudiences = validAudiences,
                ValidateIssuerSigningKey = true,
                IssuerSigningKeys = signingKeys,
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            try
            {
                return tokenHandler.ValidateToken(token, validationParameters, out _);
            }
            catch (Exception exception)
            {
                logger.LogWarning(exception, "JWT validation failed. validAudiences=[{Audiences}] issuer='{Issuer}'.", string.Join(",", validAudiences), issuer);
                throw;
            }
        }
    }
}
