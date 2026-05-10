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
    public sealed class RequireAccessAttribute : Attribute, IAuthorizationFilter
    {
        public string Description { get; }

        public RequireAccessAttribute(string description = "")
        {
            Description = description?.Trim() ?? string.Empty;
        }

        public void OnAuthorization(AuthorizationFilterContext context)
        {
            ClaimsPrincipal user = context.HttpContext.User;

            if (user.Identity?.IsAuthenticated == true)
            {
                AuthorizeUser(context, user);
                return;
            }

            AuthorizeApiKey(context);
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

        private static void AuthorizeApiKey(AuthorizationFilterContext context)
        {
            if (context.HttpContext.RequestServices is null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            ITenantResolver tenantResolver = context.HttpContext.RequestServices.GetRequiredService<ITenantResolver>();
            TenantInfo? tenant = ResolveByBasicAuth(context.HttpContext.Request, tenantResolver)
                ?? ResolveByApiKeyHeader(context.HttpContext.Request, tenantResolver);

            if (tenant is null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            SetTenantContext(context, tenant);
        }

        private static TenantInfo? ResolveByBasicAuth(HttpRequest request, ITenantResolver tenantResolver)
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

            return tenantResolver.ResolveByTenantAndApiKeyAsync(tenantId, apiKey).GetAwaiter().GetResult();
        }

        private static TenantInfo? ResolveByApiKeyHeader(HttpRequest request, ITenantResolver tenantResolver)
        {
            string? providedApiKey = request.Headers["X-Api-Key"].FirstOrDefault()
                ?? request.Headers["X-Integration-Secret"].FirstOrDefault();

            if (string.IsNullOrWhiteSpace(providedApiKey))
            {
                return null;
            }

            return tenantResolver.ResolveByApiKeyAsync(providedApiKey).GetAwaiter().GetResult();
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
