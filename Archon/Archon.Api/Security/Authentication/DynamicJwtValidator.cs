using Archon.Infrastructure.IdentityManagement;
using Archon.Infrastructure.MultiTenancy;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.IdentityModel.Tokens.Jwt;
using System.Security.Claims;

namespace Archon.Api.Security.Authentication
{
    public sealed class DynamicJwtValidator
    {
        private static int legacyAuthorityWarningLogged;

        private readonly IdentityManagementClient identityManagementClient;
        private readonly JwtOptions jwtOptions;
        private readonly IdentityCatalogOptions identityCatalogOptions;
        private readonly ILogger<DynamicJwtValidator> logger;

        public DynamicJwtValidator(
            IdentityManagementClient identityManagementClient,
            IOptions<JwtOptions> jwtOptions,
            IOptions<IdentityCatalogOptions> identityCatalogOptions,
            ILogger<DynamicJwtValidator> logger)
        {
            this.identityManagementClient = identityManagementClient;
            this.jwtOptions = jwtOptions.Value;
            this.identityCatalogOptions = identityCatalogOptions.Value;
            this.logger = logger;
        }

        public async Task<ClaimsPrincipal?> ValidateTokenAsync(string token, CancellationToken cancellationToken = default)
        {
            string? authority = ResolveAuthority();

            JwtSecurityTokenHandler tokenHandler = new JwtSecurityTokenHandler
            {
                MapInboundClaims = false
            };
            OpenIdConnectConfigurationInfo? configuration = await identityManagementClient.GetOpenIdConfigurationAsync(authority, cancellationToken);
            IReadOnlyCollection<SecurityKey> signingKeys = await identityManagementClient.GetSigningKeysAsync(authority, cancellationToken);

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

        /// <summary>
        /// Autoridade OIDC vinda de configuracao GLOBAL. E o que permite validar o token sem antes
        /// escolher um tenant: descobrir o JWKS pela tabela `integrations` obrigaria a abrir o banco de
        /// algum tenant, e o unico jeito de saber qual seria confiar num token ainda nao verificado.
        /// </summary>
        private string? ResolveAuthority()
        {
            if (!string.IsNullOrWhiteSpace(jwtOptions.Authority))
            {
                return jwtOptions.Authority;
            }

            if (!string.IsNullOrWhiteSpace(identityCatalogOptions.BaseUrl))
            {
                return identityCatalogOptions.BaseUrl;
            }

            // Sem autoridade global, resta o caminho antigo (integracao no banco do tenant), que so
            // funciona se algum tenant ja tiver sido resolvido. Nao e silencioso: avisa uma vez.
            if (Interlocked.Exchange(ref legacyAuthorityWarningLogged, 1) == 0)
            {
                logger.LogWarning(
                    "Nem Jwt:Authority nem IdentityCatalog:BaseUrl estao configurados. A descoberta do JWKS vai " +
                    "depender da integracao 'identity-management' no banco do tenant, o que exige resolver o tenant " +
                    "antes de validar o token. Configure Jwt:Authority.");
            }

            return null;
        }
    }
}
