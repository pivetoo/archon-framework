using System.Globalization;
using Archon.Api.Attributes;
using Archon.Api.Localization;
using Archon.Core.Access;
using Archon.Infrastructure.IdentityManagement;
using Microsoft.AspNetCore.Mvc.Controllers;
using Microsoft.AspNetCore.Routing.Patterns;
using Microsoft.Extensions.Localization;
using Microsoft.Extensions.Options;

namespace Archon.Api.AccessSync
{
    internal sealed class ArchonAccessSyncService
    {
        private const string SyncCulture = "pt-BR";

        private readonly EndpointDataSource endpointDataSource;
        private readonly IdentityManagementClient identityManagementClient;
        private readonly JwtOptions jwtOptions;
        private readonly IStringLocalizerFactory stringLocalizerFactory;
        private readonly LocalizationCatalogOptions localizationCatalogOptions;

        public ArchonAccessSyncService(
            EndpointDataSource endpointDataSource,
            IdentityManagementClient identityManagementClient,
            IOptions<JwtOptions> jwtOptions,
            IStringLocalizerFactory stringLocalizerFactory,
            LocalizationCatalogOptions localizationCatalogOptions)
        {
            this.endpointDataSource = endpointDataSource;
            this.identityManagementClient = identityManagementClient;
            this.jwtOptions = jwtOptions.Value;
            this.stringLocalizerFactory = stringLocalizerFactory;
            this.localizationCatalogOptions = localizationCatalogOptions;
        }

        public async Task SyncAsync(CancellationToken cancellationToken = default)
        {
            List<IStringLocalizer> localizers = localizationCatalogOptions.ResourceTypes
                .Select(stringLocalizerFactory.Create)
                .ToList();

            List<AccessResourceModel> resources;
            using (new CultureScope(SyncCulture))
            {
                resources = endpointDataSource.Endpoints
                    .OfType<RouteEndpoint>()
                    .Select(endpoint => CreateResource(endpoint, jwtOptions.Audience, localizers))
                    .Where(resource => resource is not null)
                    .Distinct(AccessResourceComparer.Instance)
                    .Cast<AccessResourceModel>()
                    .OrderBy(resource => resource.Name, StringComparer.Ordinal)
                    .ThenBy(resource => resource.HttpMethod, StringComparer.Ordinal)
                    .ToList();
            }

            await identityManagementClient.SyncAccessResourcesAsync(resources, cancellationToken);
        }

        private static AccessResourceModel? CreateResource(RouteEndpoint endpoint, string systemAudience, IReadOnlyList<IStringLocalizer> localizers)
        {
            ControllerActionDescriptor? actionDescriptor = endpoint.Metadata.GetMetadata<ControllerActionDescriptor>();
            if (actionDescriptor is null || !RequiresAccess(actionDescriptor) || string.IsNullOrWhiteSpace(systemAudience))
            {
                return null;
            }

            RequireAccessAttribute? accessAttribute =
                actionDescriptor.MethodInfo.GetCustomAttributes(typeof(RequireAccessAttribute), true).OfType<RequireAccessAttribute>().FirstOrDefault()
                ?? actionDescriptor.ControllerTypeInfo.GetCustomAttributes(typeof(RequireAccessAttribute), true).OfType<RequireAccessAttribute>().FirstOrDefault();

            AccessAreaAttribute? areaAttribute =
                actionDescriptor.ControllerTypeInfo.GetCustomAttributes(typeof(AccessAreaAttribute), true).OfType<AccessAreaAttribute>().FirstOrDefault();

            string controller = ToCamelCase(actionDescriptor.ControllerName);
            string action = ToCamelCase(actionDescriptor.ActionName);
            string accessName = $"{controller}.{action}";
            string httpMethod = endpoint.Metadata
                .OfType<HttpMethodMetadata>()
                .SelectMany(metadata => metadata.HttpMethods)
                .FirstOrDefault() ?? "GET";

            string description = ResolveTranslation(accessAttribute?.Description, localizers);
            string area = ResolveTranslation(areaAttribute?.Description, localizers);

            return new AccessResourceModel
            {
                SystemAudience = systemAudience,
                Name = accessName,
                Description = description,
                Area = area,
                Controller = controller,
                Action = action,
                HttpMethod = httpMethod,
                Route = NormalizeRoute(endpoint.RoutePattern)
            };
        }

        private static string ResolveTranslation(string? rawValue, IReadOnlyList<IStringLocalizer> localizers)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return string.Empty;
            }

            foreach (IStringLocalizer localizer in localizers)
            {
                LocalizedString localized = localizer[rawValue];
                if (!localized.ResourceNotFound)
                {
                    return localized.Value;
                }
            }

            return rawValue;
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

        private sealed class CultureScope : IDisposable
        {
            private readonly CultureInfo previousCulture;
            private readonly CultureInfo previousUiCulture;

            public CultureScope(string culture)
            {
                CultureInfo target = CultureInfo.GetCultureInfo(culture);
                previousCulture = CultureInfo.CurrentCulture;
                previousUiCulture = CultureInfo.CurrentUICulture;
                CultureInfo.CurrentCulture = target;
                CultureInfo.CurrentUICulture = target;
            }

            public void Dispose()
            {
                CultureInfo.CurrentCulture = previousCulture;
                CultureInfo.CurrentUICulture = previousUiCulture;
            }
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
