using Archon.Infrastructure.IdentityManagement;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;
using System.Text;

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
            JwtSecurityToken jwtSecurityToken = tokenHandler.ReadJwtToken(token);

            string? clientId = jwtSecurityToken.Claims.FirstOrDefault(claim => claim.Type == "client_id")?.Value;
            if (string.IsNullOrWhiteSpace(clientId))
            {
                return null;
            }

            IdentityManagementApplicationInfo? application = await identityManagementClient.GetApplicationByClientIdAsync(clientId, cancellationToken);
            if (application is null || string.IsNullOrWhiteSpace(application.JwtSecretKey))
            {
                return null;
            }

            TokenValidationParameters validationParameters = new TokenValidationParameters
            {
                ValidateIssuer = !string.IsNullOrWhiteSpace(jwtOptions.Issuer),
                ValidIssuer = jwtOptions.Issuer,
                ValidateAudience = !string.IsNullOrWhiteSpace(jwtOptions.Audience),
                ValidAudience = jwtOptions.Audience,
                ValidateIssuerSigningKey = true,
                IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(application.JwtSecretKey)),
                ValidateLifetime = true,
                ClockSkew = TimeSpan.FromMinutes(1)
            };

            return tokenHandler.ValidateToken(token, validationParameters, out _);
        }
    }
}
