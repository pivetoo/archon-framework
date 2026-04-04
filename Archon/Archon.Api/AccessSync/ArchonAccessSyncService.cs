using Archon.Api.Attributes;
using Archon.Core.Access;
using Archon.Infrastructure.IdentityManagement;
using Microsoft.Extensions.Options;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing.Patterns;

namespace Archon.Api.AccessSync
{
    internal sealed class ArchonAccessSyncService
    {
        private readonly EndpointDataSource endpointDataSource;
        private readonly IdentityManagementClient identityManagementClient;
        private readonly JwtOptions jwtOptions;

        public ArchonAccessSyncService(
            EndpointDataSource endpointDataSource,
            IdentityManagementClient identityManagementClient,
            IOptions<JwtOptions> jwtOptions)
        {
            this.endpointDataSource = endpointDataSource;
            this.identityManagementClient = identityManagementClient;
            this.jwtOptions = jwtOptions.Value;
        }

        public async Task SyncAsync(CancellationToken cancellationToken = default)
        {
            List<AccessResourceModel> resources = endpointDataSource.Endpoints
                .OfType<RouteEndpoint>()
                .Select(endpoint => CreateResource(endpoint, jwtOptions.Audience))
                .Where(resource => resource is not null)
                .Distinct(AccessResourceComparer.Instance)
                .Cast<AccessResourceModel>()
                .OrderBy(resource => resource.Name, StringComparer.Ordinal)
                .ThenBy(resource => resource.HttpMethod, StringComparer.Ordinal)
                .ToList();

            await identityManagementClient.SyncAccessResourcesAsync(resources, cancellationToken);
        }

        private static AccessResourceModel? CreateResource(RouteEndpoint endpoint, string systemAudience)
        {
            ControllerActionDescriptor? actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            if (actionDescriptor is null || !RequiresAccess(actionDescriptor) || string.IsNullOrWhiteSpace(systemAudience))
            {
                return null;
            }

            RequireAccessAttribute? accessAttribute =
                actionDescriptor.MethodInfo.GetCustomAttributes(typeof(RequireAccessAttribute), true).OfType<RequireAccessAttribute>().FirstOrDefault()
                ?? actionDescriptor.ControllerTypeInfo.GetCustomAttributes(typeof(RequireAccessAttribute), true).OfType<RequireAccessAttribute>().FirstOrDefault();

            string controller = ToCamelCase(actionDescriptor.ControllerName);
            string action = ToCamelCase(actionDescriptor.ActionName);
            string accessName = $"{controller}.{action}";
            string httpMethod = endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .SelectMany(metadata => metadata.HttpMethods)
                .FirstOrDefault() ?? "GET";

            return new AccessResourceModel
            {
                SystemAudience = systemAudience,
                Name = accessName,
                Description = accessAttribute?.Description ?? string.Empty,
                Controller = controller,
                Action = action,
                HttpMethod = httpMethod,
                Route = NormalizeRoute(endpoint.RoutePattern)
            };
        }

        private static bool RequiresAccess(ControllerActionDescriptor actionDescriptor)
        {
            return actionDescriptor.MethodInfo.IsDefined(typeof(RequireAccessAttribute), true) ||
                actionDescriptor.ControllerTypeInfo.IsDefined(typeof(RequireAccessAttribute), true);
        }

        private static string NormalizeRoute(RoutePattern routePattern)
        {
            string rawText = routePattern.RawText ?? string.Empty;
            return rawText.StartsWith("/", StringComparison.Ordinal) ? rawText : $"/{rawText}";
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

        private sealed class AccessResourceComparer : IEqualityComparer<AccessResourceModel>
        {
            public static AccessResourceComparer Instance { get; } = new AccessResourceComparer();

            public bool Equals(AccessResourceModel? x, AccessResourceModel? y)
            {
                if (ReferenceEquals(x, y))
                {
                    return true;
                }

                if (x is null || y is null)
                {
                    return false;
                }

                return string.Equals(x.SystemAudience, y.SystemAudience, StringComparison.Ordinal) &&
                    string.Equals(x.Name, y.Name, StringComparison.Ordinal) &&
                    string.Equals(x.HttpMethod, y.HttpMethod, StringComparison.Ordinal) &&
                    string.Equals(x.Route, y.Route, StringComparison.Ordinal);
            }

            public int GetHashCode(AccessResourceModel obj)
            {
                return HashCode.Combine(obj.SystemAudience, obj.Name, obj.HttpMethod, obj.Route);
            }
        }
    }
}
