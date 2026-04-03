using Archon.Application.Abstractions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.DependencyInjection;

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
                string? sessionId = context.User.FindFirst("session_id")?.Value;
                if (!string.IsNullOrWhiteSpace(sessionId))
                {
                    ISessionValidator? sessionValidator = context.RequestServices.GetService<ISessionValidator>();
                    if (sessionValidator is not null)
                    {
                        bool isValid = await sessionValidator.IsSessionValidAsync(sessionId, context.RequestAborted);
                        if (!isValid)
                        {
                            context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                            await context.Response.WriteAsJsonAsync(new
                            {
                                message = "Invalid or expired session."
                            });
                            return;
                        }
                    }
                }
            }

            await next(context);
        }
    }
}
