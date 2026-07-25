using System.Security.Claims;
using System.Text;
using Archon.Application.MultiTenancy;
using Archon.Infrastructure.MultiTenancy;
using Microsoft.AspNetCore.Http;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Mvc.Filters;

namespace Archon.Api.Attributes
{
    [AttributeUsage(AttributeTargets.Method | AttributeTargets.Class)]
    public sealed class RequireAccessAttribute : Attribute, IAsyncAuthorizationFilter
    {
        public string Description { get; }

        public RequireAccessAttribute(string description = "")
        {
            Description = description?.Trim() ?? string.Empty;
        }

        // IAsyncAuthorizationFilter, e nao IAuthorizationFilter: a resolucao de tenant por chave e
        // assincrona, e o filtro sincrono obrigava a bloquear com .GetAwaiter().GetResult() a cada
        // request nao autenticado — sob carga isso consome thread do pool sem necessidade.
        public async Task OnAuthorizationAsync(AuthorizationFilterContext context)
        {
            ClaimsPrincipal user = context.HttpContext.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                AuthorizeUser(context, user);
                return;
            }

            await AuthorizeApiKeyAsync(context);
        }

        private static void AuthorizeUser(AuthorizationFilterContext context, ClaimsPrincipal user)
        {
            if (user.HasClaim("root", "true"))
            {
                return;
            }

            if (context.ActionDescriptor is not ControllerActionDescriptor actionDescriptor)
            {
                context.Result = new ForbidResult();
                return;
            }

            string access = $"{ToCamelCase(actionDescriptor.ControllerName)}.{ToCamelCase(actionDescriptor.ActionName)}";
            if (user.HasClaim("permission", access))
            {
                return;
            }

            context.Result = new ForbidResult();
        }

        private static async Task AuthorizeApiKeyAsync(AuthorizationFilterContext context)
        {
            if (context.HttpContext.RequestServices is null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            ITenantResolver tenantResolver = context.HttpContext.RequestServices.GetRequiredService<ITenantResolver>();
            TenantInfo? tenant = await ResolveByBasicAuthAsync(context.HttpContext.Request, tenantResolver)
                ?? await ResolveByApiKeyHeaderAsync(context.HttpContext.Request, tenantResolver);

            if (tenant is null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            SetTenantContext(context, tenant);
        }

        private static async Task<TenantInfo?> ResolveByBasicAuthAsync(HttpRequest request, ITenantResolver tenantResolver)
        {
            string? authorization = request.Headers.Authorization.FirstOrDefault();
            if (string.IsNullOrWhiteSpace(authorization))
            {
                return null;
            }

            const string prefix = "Basic ";
            if (!authorization.StartsWith(prefix, StringComparison.OrdinalIgnoreCase))
            {
                return null;
            }

            string encoded = authorization[prefix.Length..].Trim();
            if (string.IsNullOrWhiteSpace(encoded))
            {
                return null;
            }

            string credentials;
            try
            {
                credentials = Encoding.UTF8.GetString(Convert.FromBase64String(encoded));
            }
            catch (FormatException)
            {
                return null;
            }

            int separator = credentials.IndexOf(':');
            if (separator <= 0 || separator == credentials.Length - 1)
            {
                return null;
            }

            string tenantId = credentials[..separator];
            string apiKey = credentials[(separator + 1)..];

            return await tenantResolver.ResolveByTenantAndApiKeyAsync(tenantId, apiKey);
        }

        private static async Task<TenantInfo?> ResolveByApiKeyHeaderAsync(HttpRequest request, ITenantResolver tenantResolver)
        {
            string? providedApiKey = request.Headers["X-Api-Key"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(providedApiKey))
            {
                return null;
            }

            return await tenantResolver.ResolveByApiKeyAsync(providedApiKey);
        }

        private static void SetTenantContext(AuthorizationFilterContext context, TenantInfo tenant)
        {
            ITenantContext tenantContext = context.HttpContext.RequestServices.GetRequiredService<ITenantContext>();
            if (tenantContext is MultiTenantContext multiTenantContext)
            {
                multiTenantContext.SetTenant(tenant);
            }

            context.HttpContext.Items["TenantId"] = tenant.TenantId;
            context.HttpContext.Items["TenantConnectionString"] = tenant.ConnectionString;
        }

        private static string ToCamelCase(string value)
        {
            if (string.IsNullOrWhiteSpace(value))
            {
                return string.Empty;
            }

            if (value.Length == 1)
            {
                return value.ToLowerInvariant();
            }

            return char.ToLowerInvariant(value[0]) + value[1..];
        }
    }
}
