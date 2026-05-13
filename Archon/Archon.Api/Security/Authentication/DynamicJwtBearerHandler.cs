using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text.Encodings.Web;
using Archon.Api.Localization;

namespace Archon.Api.Security.Authentication
{
    public sealed class DynamicJwtBearerHandler : AuthenticationHandler<AuthenticationSchemeOptions>
    {
        private readonly DynamicJwtValidator jwtValidator;

        public DynamicJwtBearerHandler(IOptionsMonitor<AuthenticationSchemeOptions> options, ILoggerFactory logger, UrlEncoder encoder, DynamicJwtValidator jwtValidator) : base(options, logger, encoder)
        {
            this.jwtValidator = jwtValidator;
        }

        protected override async Task<AuthenticateResult> HandleAuthenticateAsync()
        {
            try
            {
                string? token = ExtractToken();
                if (string.IsNullOrWhiteSpace(token))
                {
                    return AuthenticateResult.NoResult();
                }

                var principal = await jwtValidator.ValidateTokenAsync(token, Context.RequestAborted);
                if (principal is null)
                {
                    IStringLocalizer<ArchonApiResource> localizer = Context.RequestServices.GetRequiredService<IStringLocalizer<ArchonApiResource>>();
                    return AuthenticateResult.Fail(localizer["auth.token.validationFailed"]);
                }

                AuthenticationTicket ticket = new AuthenticationTicket(principal, Scheme.Name);
                return AuthenticateResult.Success(ticket);
            }
            catch (SecurityTokenException exception)
            {
                IStringLocalizer<ArchonApiResource> localizer = Context.RequestServices.GetRequiredService<IStringLocalizer<ArchonApiResource>>();
                return AuthenticateResult.Fail(localizer["auth.token.validationFailedWithDetail", exception.Message]);
            }
            catch
            {
                IStringLocalizer<ArchonApiResource> localizer = Context.RequestServices.GetRequiredService<IStringLocalizer<ArchonApiResource>>();
                return AuthenticateResult.Fail(localizer["auth.token.validationFailed"]);
            }
        }

        // Suporte nativo a SignalR: WebSocket não permite headers customizados no handshake,
        // então o client envia o token via query string para conexões em /hubs.
        private string? ExtractToken()
        {
            string? authorizationHeader = Request.Headers.Authorization.FirstOrDefault();
            if (!string.IsNullOrWhiteSpace(authorizationHeader) && authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                string headerToken = authorizationHeader["Bearer ".Length..].Trim();
                if (!string.IsNullOrWhiteSpace(headerToken))
                {
                    return headerToken;
                }
            }

            if (Request.Path.StartsWithSegments("/hubs"))
            {
                string? queryToken = Request.Query["access_token"];
                if (!string.IsNullOrWhiteSpace(queryToken))
                {
                    return queryToken;
                }
            }

            return null;
        }
    }
}
