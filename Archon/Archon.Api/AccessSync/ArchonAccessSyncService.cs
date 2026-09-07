using System.Globalization;
using System.Reflection;
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
    public sealed record AccessSyncOutcome(AccessResourceSyncResult? Resources, AccessResourceSyncResult? Capabilities, int CapabilityCount);

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

        public async Task<AccessSyncOutcome> SyncAsync(CancellationToken cancellationToken = default)
        {
            // O resource do proprio framework entra por ultimo: a aplicacao pode sobrescrever qualquer
            // chave, e os controllers do Archon (auditoria, notificacoes, usuarios) deixam de aparecer
            // com a chave crua no catalogo.
            List<IStringLocalizer> localizers = localizationCatalogOptions.ResourceTypes
                .Append(typeof(ArchonApiResource))
                .Distinct()
                .Select(stringLocalizerFactory.Create)
                .ToList();

            List<AccessResourceModel> resources;
            List<AccessCapabilityModel> catalog;
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

                AccessCatalogAttribute? catalogAttribute = Assembly.GetEntryAssembly()?.GetCustomAttribute<AccessCatalogAttribute>();
                catalog = AccessCapabilityResolver.BuildCatalog(resources, catalogAttribute, key => TryResolveTranslation(key, localizers));
            }

            AccessResourceSyncResult? resourceResult = await identityManagementClient.SyncAccessResourcesAsync(resources, cancellationToken);

            AccessResourceSyncResult? capabilityResult = null;
            if (!string.IsNullOrWhiteSpace(jwtOptions.Audience))
            {
                capabilityResult = await identityManagementClient.SyncAccessCapabilitiesAsync(new AccessCapabilitySyncRequest
                {
                    SystemAudience = jwtOptions.Audience,
                    Capabilities = catalog
                }, cancellationToken);
            }

            return new AccessSyncOutcome(resourceResult, capabilityResult, catalog.Count);
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
                Route = NormalizeRoute(endpoint.RoutePattern),
                Capabilities = AccessCapabilityResolver.Resolve(actionDescriptor.ControllerTypeInfo, actionDescriptor.MethodInfo, httpMethod).ToList()
            };
        }

        private static string ResolveTranslation(string? rawValue, IReadOnlyList<IStringLocalizer> localizers)
        {
            if (string.IsNullOrWhiteSpace(rawValue))
            {
                return string.Empty;
            }

            return TryResolveTranslation(rawValue, localizers) ?? rawValue;
        }

        private static string? TryResolveTranslation(string key, IReadOnlyList<IStringLocalizer> localizers)
        {
            foreach (IStringLocalizer localizer in localizers)
            {
                LocalizedString localized = localizer[key];
                if (!localized.ResourceNotFound)
                {
                    return localized.Value;
                }
            }

            return null;
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

        private sealed class AccessResourceComparer : IEqualityComparer<AccessResourceModel?>
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

            public int GetHashCode(AccessResourceModel? obj)
            {
                return obj is null ? 0 : HashCode.Combine(obj.SystemAudience, obj.Name, obj.HttpMethod, obj.Route);
            }
        }
    }
}
