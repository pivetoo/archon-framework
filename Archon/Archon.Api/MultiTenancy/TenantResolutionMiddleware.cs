using Archon.Application.MultiTenancy;
using Archon.Infrastructure.MultiTenancy;
using System.Text.Json;

namespace Archon.Api.MultiTenancy
{
    public sealed class TenantResolutionMiddleware
    {
        private readonly RequestDelegate next;

        public TenantResolutionMiddleware(RequestDelegate next)
        {
            this.next = next;
        }

        public async Task InvokeAsync(HttpContext context, ITenantResolver tenantResolver, ITenantContext tenantContext)
        {
            string? tenantId = context.User.FindFirst("tenant_id")?.Value;
            tenantId ??= context.User.FindFirst("contract_id")?.Value;
            tenantId ??= TryExtractTenantIdFromJwt(context);

            TenantInfo? tenant = await tenantResolver.ResolveAsync(tenantId, context.RequestAborted);

            if (tenant is null && !string.IsNullOrWhiteSpace(tenantId))
            {
                tenant = await tenantResolver.ResolveAsync(null, context.RequestAborted);
            }

            if (tenant is null)
            {
                throw new InvalidOperationException("tenant.notConfigured");
            }

            if (tenantContext is MultiTenantContext multiTenantContext)
            {
                multiTenantContext.SetTenant(tenant);
            }

            context.Items["TenantId"] = tenant.TenantId;
            context.Items["TenantConnectionString"] = tenant.ConnectionString;

            await next(context);
        }

        private static string? TryExtractTenantIdFromJwt(HttpContext context)
        {
            string? authorizationHeader = context.Request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authorizationHeader) || !authorizationHeader.StartsWith("Bearer ", StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string token = authorizationHeader["Bearer ".Length..].Trim();
            string[] segments = token.Split('.');
            if (segments.Length < 2)
            {
                return null;
            }

            try
            {
                byte[] payloadBytes = DecodeBase64Url(segments[1]);
                using JsonDocument document = JsonDocument.Parse(payloadBytes);

                if (document.RootElement.TryGetProperty("tenant_id", out JsonElement tenantIdElement))
                {
                    return tenantIdElement.GetString();
                }

                if (document.RootElement.TryGetProperty("contract_id", out JsonElement contractIdElement))
                {
                    return contractIdElement.GetString();
                }

                return null;
            }
            catch
            {
                return null;
            }
        }

        private static byte[] DecodeBase64Url(string value)
        {
            string normalized = value.Replace('-', '+').Replace('_', '/');
            int padding = 4 - normalized.Length % 4;
            if (padding is > 0 and < 4)
            {
                normalized = normalized.PadRight(normalized.Length + padding, '=');
            }

            return Convert.FromBase64String(normalized);
        }
    }
}
