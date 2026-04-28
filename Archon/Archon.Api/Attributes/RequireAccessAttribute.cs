using System.Security.Claims;
using Archon.Application.MultiTenancy;
using Archon.Infrastructure.MultiTenancy;
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

            AuthorizeIntegrationSecret(context);
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

        private static void AuthorizeIntegrationSecret(AuthorizationFilterContext context)
        {
            if (context.HttpContext.RequestServices is null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            string? providedSecret = context.HttpContext.Request.Headers["X-Integration-Secret"].FirstOrDefault();
            if (string.IsNullOrWhiteSpace(providedSecret))
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            ITenantResolver tenantResolver = context.HttpContext.RequestServices.GetRequiredService<ITenantResolver>();
            TenantInfo? tenant = tenantResolver.ResolveBySecretAsync(providedSecret).GetAwaiter().GetResult();

            if (tenant is null)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

            SetTenantContext(context, tenant);
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
