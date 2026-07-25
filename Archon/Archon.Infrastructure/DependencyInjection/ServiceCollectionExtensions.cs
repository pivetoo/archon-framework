using Archon.Application.Abstractions;
using Archon.Application.Events;
using Archon.Application.MultiTenancy;
using Archon.Application.Services;
using Archon.Core.ValueObjects;
using Archon.Infrastructure.Events;
using Archon.Infrastructure.Integrations;
using Archon.Infrastructure.IdentityManagement;
using Archon.Infrastructure.Migrations;
using Archon.Infrastructure.RestApi;
using Archon.Infrastructure.MultiTenancy;
using Archon.Infrastructure.Persistence.EF;
using Archon.Infrastructure.Services;
using Microsoft.AspNetCore.Builder;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Options;
using System.Reflection;

namespace Archon.Infrastructure.DependencyInjection
{
    public static class ServiceCollectionExtensions
    {
        public static IServiceCollection AddArchonMultiTenancy(this IServiceCollection services, IConfiguration configuration)
        {
            if (configuration is IConfigurationBuilder configBuilder)
            {
                IReadOnlyList<BootstrapTenant> tenants = TenantCatalogBootstrap.Hydrate(configuration);
                TenantCatalogBootstrap.OverlayOnConfiguration(configBuilder, tenants);
            }

            services.Configure<TenantDatabaseOptions>(configuration);
            services.Configure<IdentityCatalogOptions>(configuration.GetSection("IdentityCatalog"));
            services.AddMemoryCache();
            services.AddScoped<MultiTenantContext>();
            services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<MultiTenantContext>());
            services.AddSingleton<ConfigurationTenantResolver>();

            IdentityCatalogOptions identityCatalogOptions = new IdentityCatalogOptions();
            configuration.GetSection("IdentityCatalog").Bind(identityCatalogOptions);

            if (identityCatalogOptions.IsConfigured)
            {
                services.AddHttpClient<IdentityCatalogClient>();
                services.AddSingleton<ITenantResolver, IdentityCatalogTenantResolver>();
            }
            else
            {
                services.AddSingleton<ITenantResolver>(provider => provider.GetRequiredService<ConfigurationTenantResolver>());
            }

            return services;
        }

        public static IServiceCollection AddArchonPersistence(this IServiceCollection services, IConfiguration configuration, params Assembly[] modelAssemblies)
        {
            services.AddArchonMultiTenancy(configuration);
            services.Configure<IntegrationOptions>(configuration.GetSection("Integration"));

            TenantDatabaseOptions tenantDatabaseOptions = BindTenantDatabaseOptions(configuration);

            services.AddSingleton(tenantDatabaseOptions);
            services.AddSingleton(new ModelAssemblyRegistry(GetModelAssemblies(modelAssemblies)));

            services.AddScoped<ArchonDbContext>(provider =>
            {
                ITenantContext tenantContext = provider.GetRequiredService<ITenantContext>();
                ModelAssemblyRegistry modelAssemblyRegistry = provider.GetRequiredService<ModelAssemblyRegistry>();
                ICurrentUser? currentUser = provider.GetService<ICurrentUser>();
                IDomainEventDispatcher? domainEventDispatcher = provider.GetService<IDomainEventDispatcher>();

                (string connectionString, DatabaseProvider databaseProvider, string? schema) = ResolveCurrentTenant(tenantContext, tenantDatabaseOptions);

                DbContextOptions<ArchonDbContext> options = DbContextOptionsFactory.Create(connectionString, databaseProvider);

                return new ArchonDbContext(options, modelAssemblyRegistry, currentUser, tenantContext, domainEventDispatcher, schema);
            });

            services.AddScoped<DbContext>(provider => provider.GetRequiredService<ArchonDbContext>());
            services.AddScoped<IAuditService, AuditService>();
            services.AddScoped<IIntegrationService, IntegrationService>();
            services.AddScoped<ITenantBootstrapService, TenantBootstrapService>();
            services.AddScoped<INotificationService, NotificationService>();
            services.AddScoped<AuditService>();
            services.AddScoped(typeof(ICrudService<>), typeof(CrudService<>));
            services.AddScoped(typeof(CrudService<>));
            services.AddScoped<IDomainEventDispatcher, DomainEventDispatcher>();

            foreach (Assembly assembly in modelAssemblies.Where(a => a is not null))
            {
                services.Scan(scan => scan
                    .FromAssemblies(assembly)
                    .AddClasses(classes => classes
                        .AssignableTo(typeof(IDomainEventHandler<>)))
                    .AsImplementedInterfaces()
                    .WithScopedLifetime());
            }

            return services;
        }

        public static IServiceCollection AddArchonRestApi(this IServiceCollection services)
        {
            services.AddHttpClient<RestApi.RestApi>();
            return services;
        }

        public static IServiceCollection AddArchonIdentityManagement(this IServiceCollection services, IConfiguration configuration)
        {
            JwtOptions jwtOptions = new JwtOptions();
            configuration.GetSection("Jwt").Bind(jwtOptions);

            services.AddMemoryCache();
            services.Configure<IntegrationOptions>(configuration.GetSection("Integration"));
            services.AddScoped<IIntegrationService, IntegrationService>();
            services.AddSingleton(Options.Create(jwtOptions));
            services.AddArchonRestApi();
            services.AddScoped<IdentityManagementClient>();
            services.AddScoped<IdentityUsersClient>();

            return services;
        }

        public static IServiceCollection AddServicesFromAssembly(this IServiceCollection services, Assembly assembly)
        {
            services.Scan(scan => scan
                .FromAssemblies(assembly)
                .AddClasses(classes => classes
                    .Where(type =>
                        type.Name.EndsWith("Service", StringComparison.Ordinal) &&
                        type.Namespace?.Contains("Services", StringComparison.Ordinal) == true))
                .AsImplementedInterfaces()
                .AsSelf()
                .WithScopedLifetime());

            return services;
        }

        public static IServiceCollection RunMigrations(this IServiceCollection services, IConfiguration configuration, string schema, params Assembly[] migrationAssemblies)
        {
            // O runner e dependencia de TenantBootstrapService, que provisiona banco de tenant sob
            // demanda e e registrado independentemente desta flag. Por isso o registro vem antes do
            // early return: a flag controla se as migrations rodam no startup, nao se o runner existe.
            // Registrando depois, "RunMigrations: false" derrubava o startup com erro de DI.
            services.AddSingleton(new TenantMigrationRunner(schema, migrationAssemblies));

            if (!configuration.GetValue<bool>("RunMigrations", false))
            {
                return services;
            }

            List<(string name, string connectionString, DatabaseProvider databaseProvider)> connections = GetMigrationConnections(configuration);

            List<string> failures = [];

            foreach ((string name, string connectionString, DatabaseProvider databaseProvider) in connections)
            {
                Console.WriteLine($"Running migrations for tenant: {name}");

                try
                {
                    DatabaseMigrator.Run(connectionString, schema, databaseProvider, migrationAssemblies);
                }
                catch (Exception exception)
                {
                    // Nao interrompe o laco: tentar todos os tenants faz o operador ver TODOS os
                    // problemas de uma vez, em vez de descobrir um por deploy.
                    Console.WriteLine($"Migration failed for tenant {name}: {exception.Message}");
                    failures.Add($"{name}: {exception.Message}");
                }
            }

            // Falhar o startup e proposital. Antes daqui a excecao era apenas impressa e a aplicacao
            // subia contra schema parcialmente migrado — o que corrompe dado em silencio. Aplicacao
            // fora do ar e visivel na hora; schema incompleto so aparece quando o estrago ja aconteceu.
            if (failures.Count > 0)
            {
                throw new InvalidOperationException(
                    $"Migration failed for {failures.Count} tenant(s) and the application will not start: {string.Join(" | ", failures)}");
            }

            return services;
        }

        private static TenantDatabaseOptions BindTenantDatabaseOptions(IConfiguration configuration)
        {
            TenantDatabaseOptions tenantDatabaseOptions = new TenantDatabaseOptions();
            configuration.Bind(tenantDatabaseOptions);

            return tenantDatabaseOptions;
        }

        private static IReadOnlyCollection<Assembly> GetModelAssemblies(IEnumerable<Assembly> modelAssemblies)
        {
            List<Assembly> assemblies = AppDomain.CurrentDomain.GetAssemblies()
                .Where(assembly => !assembly.IsDynamic)
                .ToList();

            foreach (Assembly assembly in modelAssemblies.Where(assembly => assembly is not null))
            {
                if (!assemblies.Contains(assembly))
                {
                    assemblies.Add(assembly);
                }
            }

            return assemblies;
        }

        private static List<(string name, string connectionString, DatabaseProvider databaseProvider)> GetMigrationConnections(IConfiguration configuration)
        {
            TenantDatabaseOptions tenantDatabaseOptions = BindTenantDatabaseOptions(configuration);

            return tenantDatabaseOptions.TenantDatabases
                .Select(item => (item.Key, item.Value.ConnectionString, item.Value.GetDatabaseProvider()))
                .Where(item => !string.IsNullOrWhiteSpace(item.ConnectionString))
                .ToList();
        }

        internal static (string connectionString, DatabaseProvider databaseProvider, string? schema) ResolveCurrentTenant(ITenantContext tenantContext, TenantDatabaseOptions tenantDatabaseOptions)
        {
            if (!string.IsNullOrWhiteSpace(tenantContext.ConnectionString))
            {
                return (tenantContext.ConnectionString, tenantContext.DatabaseProvider, tenantContext.Schema);
            }

            // Nenhum tenant foi resolvido para este escopo. So e seguro assumir um tenant
            // implicitamente quando ha EXATAMENTE UM configurado (single-tenant / FixedTenant) -
            // ai nao existe outro tenant a ser contaminado. Com 2+ tenants configurados, assumir o
            // "primeiro" operaria sobre o tenant ERRADO; por isso falha explicitamente em vez de cair
            // num fallback silencioso.
            List<TenantDatabaseOption> configured = tenantDatabaseOptions.TenantDatabases
                .Select(entry => entry.Value)
                .Where(value => value is not null && !string.IsNullOrWhiteSpace(value.ConnectionString))
                .ToList();

            if (configured.Count == 1)
            {
                TenantDatabaseOption single = configured[0];
                return (single.ConnectionString, single.GetDatabaseProvider(), single.Schema);
            }

            if (configured.Count == 0)
            {
                throw new InvalidOperationException("No tenant connection string was configured for the current request.");
            }

            throw new InvalidOperationException(
                "No tenant was resolved for the current scope and multiple tenants are configured. " +
                "Refusing to fall back to an arbitrary tenant to avoid operating on the wrong tenant's data.");
        }

    }
}
