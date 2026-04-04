using System.Security.Claims;
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

            if (user.Identity?.IsAuthenticated != true)
            {
                context.Result = new UnauthorizedResult();
                return;
            }

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
