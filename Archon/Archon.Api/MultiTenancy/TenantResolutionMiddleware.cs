using Archon.Application.MultiTenancy;
using Archon.Infrastructure.MultiTenancy;
using Microsoft.Extensions.Configuration;

namespace Archon.Api.MultiTenancy
{
    /// <summary>
    /// Resolve o tenant do request a partir de claim JA VALIDADA. Precisa ser registrado DEPOIS de
    /// <c>UseAuthentication()</c>, via <c>UseArchonTenantResolution()</c>.
    ///
    /// Antes este middleware rodava antes da autenticacao e, sem claims disponiveis, decodificava o
    /// payload do Bearer na mao para ler `tenant_id` — sem verificar a assinatura. Como o tenant define
    /// a connection string do request, um token forjado apontando outro tenant fazia rota anonima operar
    /// no banco alheio. Ler apenas claim validada elimina isso.
    /// </summary>
    public sealed class TenantResolutionMiddleware
    {
        private readonly RequestDelegate next;

        public TenantResolutionMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        private const string FixedTenantKey = "FixedTenantId";

        public async Task InvokeAsync(HttpContext context, ITenantResolver tenantResolver, ITenantContext tenantContext, IConfiguration configuration)
        {
            bool isFixedMode = configuration.GetSection("TenantDatabases").GetChildren()
                .Any(section => string.Equals(section.Key, FixedTenantKey, StringComparison.OrdinalIgnoreCase));

            string? tenantId = isFixedMode
                ? FixedTenantKey
                : context.User.FindFirst("tenant_id")?.Value
                    ?? context.User.FindFirst("contract_id")?.Value;

            if (string.IsNullOrWhiteSpace(tenantId))
            {
                // Sem claim validada de tenant: server-to-server com X-Api-Key/Basic Auth, ou rota anonima.
                // O RequireAccessAttribute popula o tenant no primeiro caso; rota publica resolve pelo
                // proprio token de rota, no middleware de tenant publico da aplicacao consumidora.
                await next(context);
                return;
            }

            TenantInfo? tenant = await tenantResolver.ResolveAsync(tenantId, context.RequestAborted);

            if (tenant is null)
            {
                // tenantId presente mas nao reconhecido: erro explicito, sem fallback.
                context.Response.StatusCode = StatusCodes.Status401Unauthorized;
                await context.Response.WriteAsJsonAsync(new { message = "tenant.notFound", tenantId });
                return;
            }

            if (tenantContext is MultiTenantContext multiTenantContext)
            {
                multiTenantContext.SetTenant(tenant);
            }

            context.Items["TenantId"] = tenant.TenantId;
            context.Items["TenantConnectionString"] = tenant.ConnectionString;

            await next(context);
        }
    }
}
