using Archon.Api.AccessSync;
using Archon.Api.ExceptionHandling;
using Archon.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;

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

        public static async Task<WebApplication> UseArchonAccessSyncAsync(this WebApplication app, CancellationToken cancellationToken = default)
        {
            using IServiceScope scope = app.Services.CreateScope();
            ArchonAccessSyncService accessSyncService = scope.ServiceProvider.GetRequiredService<ArchonAccessSyncService>();
            await accessSyncService.SyncAsync(cancellationToken);

            return app;
        }
    }
}
