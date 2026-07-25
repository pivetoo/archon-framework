using Archon.Api.AccessSync;
using Archon.Api.ExceptionHandling;
using Archon.Api.Security;
using Microsoft.AspNetCore.Builder;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;

namespace Archon.Api.MultiTenancy
{
    public static class ApplicationBuilderExtensions
    {
        public static IApplicationBuilder UseArchonApi(this IApplicationBuilder app)
        {
            app.UseRequestLocalization();
            app.UseMiddleware<ExceptionHandlingMiddleware>();

            return app;
        }

        /// <summary>
        /// Resolucao de tenant a partir de claim validada. Registrar DEPOIS de <c>UseAuthentication()</c>.
        /// Ficava dentro de <c>UseArchonApi()</c>, que roda antes da autenticacao — e por isso o middleware
        /// era obrigado a ler o `tenant_id` de um JWT sem assinatura verificada.
        /// </summary>
        public static IApplicationBuilder UseArchonTenantResolution(this IApplicationBuilder app)
        {
            return app.UseMiddleware<TenantResolutionMiddleware>();
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
            app.Lifetime.ApplicationStarted.Register(() =>
            {
                _ = Task.Run(async () =>
                {
                    using IServiceScope scope = app.Services.CreateScope();
                    ILogger logger = scope.ServiceProvider
                        .GetRequiredService<ILoggerFactory>()
                        .CreateLogger("ArchonAccessSync");

                    try
                    {
                        ArchonAccessSyncService accessSyncService = ActivatorUtilities.CreateInstance<ArchonAccessSyncService>(scope.ServiceProvider);
                        await accessSyncService.SyncAsync(cancellationToken);
                    }
                    catch (Exception exception)
                    {
                        logger.LogError(exception, "An error occurred while synchronizing access resources with IdentityManagement.");
                    }
                }, cancellationToken);
            });

            return app;
        }
    }
}
