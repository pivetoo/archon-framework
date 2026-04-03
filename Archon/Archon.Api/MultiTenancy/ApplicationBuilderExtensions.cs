using Archon.Api.ExceptionHandling;
using Archon.Api.Security;
using Microsoft.AspNetCore.Builder;

namespace Archon.Api.MultiTenancy
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseArchonApi(this IApplicationBuilder app)
        {
            app.UseMiddleware<ExceptionHandlingMiddleware>();
            app.UseMiddleware<TenantResolutionMiddleware>();

            return app;
        }

        public static IApplicationBuilder UseSessionValidation(this IApplicationBuilder app)
        {
            return app.UseMiddleware<SessionValidationMiddleware>();
        }

        public static IApplicationBuilder UseIdentityManagementUserSync(this IApplicationBuilder app)
        {
            return app.UseMiddleware<IdentityManagementUserSyncMiddleware>();
        }
    }
}
