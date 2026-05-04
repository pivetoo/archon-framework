using Archon.Application.Abstractions;
using Archon.Application.Events;
using Archon.Application.MultiTenancy;
using Archon.Application.Services;
using Archon.Core.ValueObjects;
using Archon.Infrastructure.BackgroundJobs;
using Archon.Infrastructure.Events;
using Archon.Infrastructure.Integrations;
using Archon.Infrastructure.IdentityManagement;
using Archon.Infrastructure.Migrations;
using Archon.Infrastructure.MultiTenancy;
using Archon.Infrastructure.Persistence.EF;
using Archon.Infrastructure.Services;
using Hangfire;
using Hangfire.PostgreSql;
using Npgsql;
using Hangfire.SqlServer;
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
            services.Configure<TenantDatabaseOptions>(configuration);
            services.Configure<TenantCatalogOptions>(configuration.GetSection("TenantCatalog"));
            services.AddMemoryCache();
            services.AddScoped<MultiTenantContext>();
            services.AddScoped<ITenantContext>(provider => provider.GetRequiredService<MultiTenantContext>());
            services.AddSingleton<ITenantResolver, ConfigurationTenantResolver>();

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

        public static IServiceCollection AddArchonIdentityManagement(this IServiceCollection services, IConfiguration configuration)
        {
            JwtOptions jwtOptions = new JwtOptions();
            configuration.GetSection("Jwt").Bind(jwtOptions);

            services.AddMemoryCache();
            services.Configure<IntegrationOptions>(configuration.GetSection("Integration"));
            services.AddScoped<IIntegrationService, IntegrationService>();
            services.AddSingleton(Options.Create(jwtOptions));
            services.AddHttpClient<IdentityManagementClient>();

            return services;
        }

        public static IServiceCollection AddArchonHangfire(this IServiceCollection services, IConfiguration configuration)
        {
            TenantDatabaseOptions tenantDatabaseOptions = BindTenantDatabaseOptions(configuration);
            (string connectionString, DatabaseProvider databaseProvider) = GetHangfireStorage(tenantDatabaseOptions);

            services.AddHangfire(config =>
            {
                config.SetDataCompatibilityLevel(CompatibilityLevel.Version_180)
                    .UseSimpleAssemblyNameTypeSerializer()
                    .UseRecommendedSerializerSettings();

                switch (databaseProvider)
                {
                    case DatabaseProvider.PostgreSql:
                        config.UsePostgreSqlStorage(options => options.UseNpgsqlConnection(connectionString));
                        break;

                    case DatabaseProvider.SqlServer:
                        config.UseSqlServerStorage(connectionString, new SqlServerStorageOptions());
                        break;

                    case DatabaseProvider.MySql:
                        throw new NotSupportedException("Hangfire storage for MySql is not supported by Archon yet.");

                    default:
                        throw new ArgumentOutOfRangeException(nameof(databaseProvider), databaseProvider, "Unsupported Hangfire storage provider.");
                }
            });

            services.AddHangfireServer();
            services.AddSingleton<IBackgroundJobService, HangfireBackgroundJobService>();

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

        public static IApplicationBuilder UseArchonHangfire(this IApplicationBuilder app, string dashboardPath = "/hangfire")
        {
            app.UseHangfireDashboard(dashboardPath);

            return app;
        }

        public static IServiceCollection RunMigrations(this IServiceCollection services, IConfiguration configuration, string schema, params Assembly[] migrationAssemblies)
        {
            if (!configuration.GetValue<bool>("RunMigrations", false))
            {
                return services;
            }

            List<(string name, string connectionString, DatabaseProvider databaseProvider)> connections = GetMigrationConnections(configuration);

            foreach ((string name, string connectionString, DatabaseProvider databaseProvider) in connections)
            {
                Console.WriteLine($"Running migrations for tenant: {name}");

                try
                {
                    DatabaseMigrator.Run(connectionString, schema, databaseProvider, migrationAssemblies);
                }
                catch (Exception exception)
                {
                    Console.WriteLine($"Migration failed for tenant {name}: {exception.Message}");
                }
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

            List<(string name, string connectionString, DatabaseProvider databaseProvider)> connections = tenantDatabaseOptions.TenantDatabases
                .Select(item => (item.Key, item.Value.ConnectionString, item.Value.GetDatabaseProvider()))
                .Where(item => !string.IsNullOrWhiteSpace(item.ConnectionString))
                .ToList();

            if (connections.Count > 0)
            {
                return connections;
            }

            return TryGetCatalogConnections(configuration);
        }

        private static List<(string name, string connectionString, DatabaseProvider databaseProvider)> TryGetCatalogConnections(IConfiguration configuration)
        {
            string? catalogConnectionString = configuration["TenantCatalog:ConnectionString"];
            string? applicationId = configuration["TenantCatalog:ApplicationId"];

            if (string.IsNullOrWhiteSpace(catalogConnectionString) || string.IsNullOrWhiteSpace(applicationId))
            {
                return [];
            }

            try
            {
                using NpgsqlConnection conn = new(catalogConnectionString);
                conn.Open();

                using NpgsqlCommand cmd = conn.CreateCommand();
                cmd.CommandText =
                    """
                    SELECT connectionstring, databasetype, coalesce("schema", 'public')
                    FROM public.tenantdatabases
                    WHERE applicationid = @appid AND isactive = true
                    """;
                cmd.Parameters.AddWithValue("appid", NpgsqlTypes.NpgsqlDbType.Text, applicationId);

                List<(string, string, DatabaseProvider)> catalogConnections = [];
                using NpgsqlDataReader reader = cmd.ExecuteReader();
                while (reader.Read())
                {
                    string connStr = reader.GetString(0);
                    string dbType = reader.GetString(1);
                    string schema = reader.GetString(2);

                    DatabaseProvider provider = dbType.Equals("PostgreSql", StringComparison.OrdinalIgnoreCase)
                        ? DatabaseProvider.PostgreSql
                        : DatabaseProvider.SqlServer;

                    catalogConnections.Add(($"catalog-{schema}", connStr, provider));
                }

                return catalogConnections;
            }
            catch (Exception ex)
            {
                Console.WriteLine($"Failed to load migration connections from TenantCatalog: {ex.Message}");
                return [];
            }
        }

        private static (string connectionString, DatabaseProvider databaseProvider, string? schema) ResolveCurrentTenant(ITenantContext tenantContext, TenantDatabaseOptions tenantDatabaseOptions)
        {
            if (!string.IsNullOrWhiteSpace(tenantContext.ConnectionString))
            {
                return (tenantContext.ConnectionString, tenantContext.DatabaseProvider, tenantContext.Schema);
            }

            KeyValuePair<string, TenantDatabaseOption> fallbackTenant = tenantDatabaseOptions.TenantDatabases
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Value.ConnectionString));

            if (!string.IsNullOrWhiteSpace(fallbackTenant.Value?.ConnectionString))
            {
                return (
                    fallbackTenant.Value.ConnectionString,
                    fallbackTenant.Value.GetDatabaseProvider(),
                    fallbackTenant.Value.Schema);
            }

            throw new InvalidOperationException("No tenant connection string was configured for the current request.");
        }

        private static (string connectionString, DatabaseProvider databaseProvider) GetHangfireStorage(TenantDatabaseOptions tenantDatabaseOptions)
        {
            KeyValuePair<string, TenantDatabaseOption> tenant = tenantDatabaseOptions.TenantDatabases
                .FirstOrDefault(item => !string.IsNullOrWhiteSpace(item.Value.ConnectionString));

            if (!string.IsNullOrWhiteSpace(tenant.Value?.ConnectionString))
            {
                return (tenant.Value.ConnectionString, tenant.Value.GetDatabaseProvider());
            }

            throw new InvalidOperationException("No connection string found for Hangfire storage. Configure TenantDatabases with at least one valid connection.");
        }
    }
}
