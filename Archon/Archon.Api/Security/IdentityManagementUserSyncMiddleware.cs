using Archon.Application.Abstractions;
using Microsoft.AspNetCore.Http;

namespace Archon.Api.Security
{
    public sealed class IdentityManagementUserSyncMiddleware
    {
        private readonly RequestDelegate next;

        public IdentityManagementUserSyncMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext context, IIdentityManagementUserSyncService userSyncService)
        {
            if (context.User.Identity?.IsAuthenticated == true)
            {
                string? userIdClaim = context.User.FindFirst("user_id")?.Value;
                string? name = context.User.FindFirst("name")?.Value;
                string? email = context.User.FindFirst("email")?.Value;

                if (long.TryParse(userIdClaim, out long externalUserId) && !string.IsNullOrWhiteSpace(name))
                {
                    object? user = await userSyncService.GetOrCreateAsync(externalUserId, name, email, context.RequestAborted);
                    context.Items["IdentityManagementUser"] = user;
                }
            }

            await next(context);
        }
    }
}
