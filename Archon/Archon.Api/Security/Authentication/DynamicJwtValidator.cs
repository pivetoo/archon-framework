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
            if (string.IsNullOrWhiteSpace(token))
            {
                throw new SecurityTokenException("Token is null or empty.");
            }

            try
            {
                JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler
                {
                    MapInboundClaims = false
                };

                JwtSecurityToken jsonToken = tokenHandler.ReadJwtToken(token);
                string? clientId = jsonToken.Claims.FirstOrDefault(claim => claim.Type == "client_id")?.Value;
                if (string.IsNullOrWhiteSpace(clientId))
                {
                    throw new SecurityTokenException("Token does not contain client_id.");
                }

                IdentityManagementApplicationInfo? application = await identityManagementClient.GetApplicationByClientIdAsync(clientId, cancellationToken);
                if (application is null || !application.IsActive)
                {
                    throw new SecurityTokenException($"Application with client_id '{clientId}' was not found or is inactive.");
                }

                TokenValidationParameters validationParameters = new TokenValidationParameters
                {
                    ValidateIssuerSigningKey = true,
                    IssuerSigningKey = new SymmetricSecurityKey(Encoding.UTF8.GetBytes(application.JwtSecretKey)),
                    ValidateIssuer = true,
                    ValidIssuer = jwtOptions.Issuer,
                    ValidateAudience = true,
                    ValidAudience = jwtOptions.Audience,
                    ValidateLifetime = true,
                    ClockSkew = TimeSpan.FromMinutes(5),
                    RequireExpirationTime = true,
                    RequireSignedTokens = true
                };

                ClaimsPrincipal principal = tokenHandler.ValidateToken(token, validationParameters, out _);
                return principal;
            }
            catch (SecurityTokenException)
            {
                throw;
            }
            catch (Exception exception)
            {
                throw new SecurityTokenException($"Token validation failed: {exception.Message}", exception);
            }
        }
    }
}
