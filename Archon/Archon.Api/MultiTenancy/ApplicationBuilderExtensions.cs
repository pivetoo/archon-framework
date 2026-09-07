using Archon.Api.AccessSync;
using Archon.Api.ExceptionHandling;
using Archon.Api.Security;
using Archon.Application.Abstractions;
using Archon.Core.Access;
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

        /// <summary>
        /// Exige uma implementacao de <see cref="ISessionValidator"/> registrada. Sem ela o middleware
        /// rodava e nao validava NADA — a aplicacao subia dando a impressao de barrar sessao revogada,
        /// sem barrar coisa alguma. Falhar aqui, no startup, torna a lacuna impossivel de ignorar.
        /// </summary>
        public static IApplicationBuilder UseSessionValidation(this IApplicationBuilder app)
        {
            if (app.ApplicationServices.GetService<ISessionValidator>() is null)
            {
                throw new InvalidOperationException(
                    "UseSessionValidation() exige uma implementacao de ISessionValidator registrada no container. " +
                    "Sem ela o middleware nao valida sessao alguma. Registre a implementacao ou remova a chamada.");
            }

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
                        AccessSyncOutcome outcome = await accessSyncService.SyncAsync(cancellationToken);
                        AccessResourceSyncResult? result = outcome.Resources;

                        if (result is null)
                        {
                            logger.LogInformation("Access resources synchronized with IdentityManagement (capabilities={Capabilities}).", outcome.CapabilityCount);
                        }
                        else
                        {
                            logger.LogInformation(
                                "Access resources synchronized with IdentityManagement: total={Total}, created={Created}, updated={Updated}, deactivated={Deactivated}, capabilities={Capabilities}.",
                                result.TotalCount, result.CreatedCount, result.UpdatedCount, result.DeactivatedCount, outcome.CapabilityCount);
                        }
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
