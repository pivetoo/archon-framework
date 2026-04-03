using Microsoft.AspNetCore.Authentication;
using Microsoft.Extensions.Logging;
using Microsoft.Extensions.Options;
using Microsoft.IdentityModel.Tokens;
using System.Text.Encodings.Web;

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
                string? authorizationHeader = Request.Headers.Authorization.FirstOrDefault();
                if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
                {
                    return AuthenticateResult.NoResult();
                }

                string token = authorizationHeader["Bearer ".Length..].Trim();
                if (string.IsNullOrWhiteSpace(token))
                {
                    return AuthenticateResult.NoResult();
                }

                var principal = await jwtValidator.ValidateTokenAsync(token, Context.RequestAborted);
                if (principal is null)
                {
                    return AuthenticateResult.Fail("Token validation failed.");
                }

                AuthenticationTicket ticket = new AuthenticationTicket(principal, Scheme.Name);
                return AuthenticateResult.Success(ticket);
            }
            catch (SecurityTokenException exception)
            {
                return AuthenticateResult.Fail($"Token validation failed: {exception.Message}");
            }
            catch
            {
                return AuthenticateResult.Fail("Token validation failed.");
            }
        }
    }
}
