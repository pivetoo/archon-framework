using Archon.Application.Abstractions;
using Archon.Api.Localization;
using Archon.Core.Responses;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Localization;

namespace Archon.Api.Security
{
    public sealed class SessionValidationMiddleware
    {
        private readonly RequestDelegate next;

        public SessionValidationMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext context)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                string? sessionId = context.User.FindFirst("sid")?.Value;
                sessionId ??= context.User.FindFirst("session_id")?.Value;
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    // GetRequiredService de proposito: a ausencia do validador ja e barrada no startup
                    // por UseSessionValidation(). Antes era GetService, e um container sem a
                    // implementacao fazia o middleware seguir sem validar nada, em silencio.
                    ISessionValidator sessionValidator = context.RequestServices.GetRequiredService<ISessionValidator>();

                    bool isValid = await sessionValidator.IsSessionValidAsync(sessionId, context.RequestAborted);
                    if (!isValid)
                    {
                        IStringLocalizer<ArchonApiResource> localizer = context.RequestServices.GetRequiredService<IStringLocalizer<ArchonApiResource>>();
                        context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                        context.Response.ContentType = "application/json";
                        await context.Response.WriteAsJsonAsync(new ApiResponse
                        {
                            Message = localizer["auth.session.invalidOrExpired"]
                        });
                        return;
                    }
                }
            }

            await next(context);
        }
    }
}
